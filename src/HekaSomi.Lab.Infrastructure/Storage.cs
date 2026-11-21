using HekaSomi.Lab.Core;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace HekaSomi.Lab.Infrastructure;

public interface IContentProtector
{
    string Protect(string plaintext); string Unprotect(string ciphertext);
}
public sealed class AesContentProtector(byte[] key) : IContentProtector
{
    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var source = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[source.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, source, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }
    public string Unprotect(string ciphertext)
    {
        var bytes = Convert.FromBase64String(ciphertext);
        var output = new byte[bytes.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), output);
        return Encoding.UTF8.GetString(output);
    }
}
public sealed partial class LabRepository(string databasePath, IContentProtector protector)
{
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout=5000; PRAGMA secure_delete=ON; CREATE TABLE IF NOT EXISTS documents (kind TEXT NOT NULL, id TEXT NOT NULL, state TEXT NOT NULL, payload TEXT NOT NULL, updated TEXT NOT NULL, PRIMARY KEY(kind,id)); PRAGMA user_version=1;";
        command.ExecuteNonQuery();
        return connection;
    }
    public Task<List<T>> ListAsync<T>(string kind)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT payload FROM documents WHERE kind=$kind ORDER BY updated DESC";
        cmd.Parameters.AddWithValue("$kind", kind);
        using var reader = cmd.ExecuteReader();
        List<T> result = [];
        while (reader.Read())
            result.Add(JsonDefaults.Read<T>(protector.Unprotect(reader.GetString(0))));
        return Task.FromResult(result);
    }
    public Task SaveRunAsync(ComparisonRun run) => PutAsync("run", run.Id, run.State, run);
    public Task SaveReviewAsync(ReviewRating rating) => PutAsync("review", $"{rating.RunId}:{rating.CaseId}:{rating.Arm}", "Reviewed", rating);
    public Task SealCaseAsync(ValidationStimulus stimulus, SealedAnswers answers)
    {
        if (stimulus.Id != answers.CaseId || stimulus.OwnerId != answers.OwnerId || !stimulus.Options.Contains(answers.T0) || answers.T1 is not null)
            throw new InvalidDataException("The sealed answer must match the case owner and one option.");
        if (string.IsNullOrWhiteSpace(stimulus.Question) || stimulus.Options.Distinct().Count() < 2)
            throw new InvalidDataException("A case needs a question and at least two options.");
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var row in new[] { ("stimulus", JsonDefaults.Write(stimulus)), ("answer", JsonDefaults.Write(answers)) })
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO documents(kind,id,state,payload,updated) VALUES($kind,$id,'Sealed',$payload,$updated)";
            cmd.Parameters.AddWithValue("$kind", row.Item1);
            cmd.Parameters.AddWithValue("$id", stimulus.Id);
            cmd.Parameters.AddWithValue("$payload", protector.Protect(row.Item2));
            cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return Task.CompletedTask;
    }
    public async Task SaveAnswersAsync(SealedAnswers answers)
    {
        var original = (await ListAsync<SealedAnswers>("answer")).SingleOrDefault(a => a.CaseId == answers.CaseId) ?? throw new InvalidOperationException("Seal the case first.");
        var stimulus = (await ListAsync<ValidationStimulus>("stimulus")).Single(c => c.Id == answers.CaseId);
        if (original.T0 != answers.T0 || original.OwnerId != answers.OwnerId || (original.T1 is not null && original.T1 != answers.T1))
            throw new InvalidOperationException("Sealed answers cannot be rewritten.");
        if (answers.T1 is not null && !stimulus.Options.Contains(answers.T1))
            throw new InvalidOperationException("T1 must match one option.");
        await PutAsync("answer", answers.CaseId, "Sealed", answers);
    }
    public Task SavePredictionAsync(PersonaPrediction prediction) => PutAsync("prediction", $"{prediction.CaseId}:{prediction.ConstructId}:{prediction.ConstructVersion}:{prediction.Condition}", "Predicted", prediction, protectExisting: true);
    public async Task SaveDraftAsync(PersonalityConstruct construct)
    {
        if (construct.Status != ConstructStatus.Draft)
            throw new InvalidOperationException("Use the approval action to activate a construct.");
        await PutAsync("construct", construct.Key, construct.Status.ToString(), construct, protectApproved: true);
    }
    public async Task ApproveAsync(PersonalityConstruct construct)
    {
        ConstructCompiler.ValidateForApproval(construct);
        if (construct.Status != ConstructStatus.Draft)
            throw new InvalidOperationException("Only a draft can be approved.");
        var approved = construct with
        {
            Status = ConstructStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow
        };
        await PutAsync("construct", approved.Key, approved.Status.ToString(), approved, protectApproved: true);
    }
    public async Task RevokeAsync(PersonalityConstruct construct)
    {
        var saved = (await ListAsync<PersonalityConstruct>("construct")).FirstOrDefault(p => p.Key == construct.Key) ?? throw new InvalidOperationException("Construct not found.");
        if (saved.Status != ConstructStatus.Approved)
            throw new InvalidOperationException("Only an approved version can be revoked.");
        await PutAsync("construct", saved.Key, "Revoked", saved with
        {
            Status = ConstructStatus.Revoked
        });
    }
    public async Task<List<string>> DeleteConstructAsync(string id)
    {
        var constructs = await ListAsync<PersonalityConstruct>("construct");
        var versions = constructs.Where(p => p.Id == id).ToArray();
        var owners = versions.Select(p => p.OwnerId).ToHashSet();
        var runIds = (await ListAsync<ComparisonRun>("run")).Where(r => r.ConstructId == id).Select(r => r.Id).ToHashSet();
        var remove = new List<(string Kind, string Id)>();
        remove.AddRange(versions.Select(p => ("construct", p.Key)));
        remove.AddRange(runIds.Select(i => ("run", i)));
        remove.AddRange((await ListAsync<ReviewRating>("review")).Where(p => runIds.Contains(p.RunId)).Select(p => ("review", $"{p.RunId}:{p.CaseId}:{p.Arm}")));
        remove.AddRange((await ListAsync<PersonaPrediction>("prediction")).Where(p => p.ConstructId == id).Select(p => ("prediction", $"{p.CaseId}:{p.ConstructId}:{p.ConstructVersion}:{p.Condition}")));
        var unsharedOwners = owners.Where(o => !constructs.Any(p => p.OwnerId == o && p.Id != id)).ToHashSet();
        remove.AddRange((await ListAsync<ValidationStimulus>("stimulus")).Where(p => unsharedOwners.Contains(p.OwnerId)).Select(p => ("stimulus", p.Id)));
        remove.AddRange((await ListAsync<SealedAnswers>("answer")).Where(p => unsharedOwners.Contains(p.OwnerId)).Select(p => ("answer", p.CaseId)));
        var participationSessions = (await ListAsync<AssessmentSession>("participation-session")).Where(s => s.ConstructId == id).Select(s => s.Id).ToHashSet();
        var participationRuns = (await ListAsync<ParticipationRun>("participation-run")).Where(r => participationSessions.Contains(r.SessionId)).Select(r => r.Id).ToHashSet();
        remove.AddRange(participationSessions.Select(s => ("participation-session", s)));
        remove.AddRange(participationRuns.Select(r => ("participation-run", r)));
        remove.AddRange((await ListAsync<ParticipationResponse>("participation-response")).Where(r => participationRuns.Contains(r.RunId)).Select(r => ("participation-response", $"{r.RunId}:{r.ItemId}")));
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var row in remove)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM documents WHERE kind=$kind AND id=$id";
            cmd.Parameters.AddWithValue("$kind", row.Kind);
            cmd.Parameters.AddWithValue("$id", row.Id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return versions.SelectMany(p => p.Evidence).Select(e => e.AudioPath).OfType<string>().Distinct().ToList();
    }
    private Task PutAsync<T>(string kind, string id, string state, T data, bool protectApproved = false, bool protectExisting = false)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        if (protectApproved || protectExisting)
        {
            using var check = c.CreateCommand();
            check.Transaction = tx;
            check.CommandText = "SELECT state FROM documents WHERE kind=$kind AND id=$id";
            check.Parameters.AddWithValue("$kind", kind);
            check.Parameters.AddWithValue("$id", id);
            var existing = check.ExecuteScalar() as string;
            if ((protectExisting && existing is not null) || existing is "Approved" or "Revoked")
                throw new InvalidOperationException("Approved/revoked versions are immutable. Create a new draft revision.");
        }
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO documents(kind,id,state,payload,updated) VALUES($kind,$id,$state,$payload,$updated) ON CONFLICT(kind,id) DO UPDATE SET state=$state,payload=$payload,updated=$updated";
        cmd.Parameters.AddWithValue("$kind", kind);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$state", state);
        cmd.Parameters.AddWithValue("$payload", protector.Protect(JsonDefaults.Write(data)));
        cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
        tx.Commit();
        return Task.CompletedTask;
    }
}
