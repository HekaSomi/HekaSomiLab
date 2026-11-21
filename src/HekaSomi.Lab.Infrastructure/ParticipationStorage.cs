using HekaSomi.Lab.Core;
using Microsoft.Data.Sqlite;

namespace HekaSomi.Lab.Infrastructure;

public sealed partial class LabRepository
{
    private T ReadDocument<T>(SqliteConnection c, SqliteTransaction tx, string kind, string id)
    {
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT payload FROM documents WHERE kind=$kind AND id=$id";
        cmd.Parameters.AddWithValue("$kind", kind); cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() is string json ? JsonDefaults.Read<T>(protector.Unprotect(json))
            : throw new InvalidOperationException("The requested record no longer exists.");
    }
    private void InsertDocument<T>(SqliteConnection c, SqliteTransaction tx, string kind, string id, T value)
    {
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO documents(kind,id,state,payload,updated) VALUES($kind,$id,'Sealed',$payload,$updated)";
        cmd.Parameters.AddWithValue("$kind", kind); cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$payload", protector.Protect(JsonDefaults.Write(value)));
        cmd.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O")); cmd.ExecuteNonQuery();
    }
    private PersonalityConstruct RequireParticipationProfile(SqliteConnection c, SqliteTransaction tx, AssessmentSession s)
    {
        var p = ReadDocument<PersonalityConstruct>(c, tx, "construct", $"{s.ConstructId}:{s.ConstructVersion}");
        if (p.Status != ConstructStatus.Approved || p.OwnerId != s.OwnerId || Metrics.Hash(JsonDefaults.Write(p)) != s.ConstructHash)
            throw new InvalidOperationException("This profile changed or was revoked. Stop the run and use an approved version.");
        return p;
    }
    public Task SealParticipationAsync(AssessmentSession session)
    {
        session.Validate();
        using var c = Open(); using var tx = c.BeginTransaction();
        RequireParticipationProfile(c, tx, session);
        using (var check = c.CreateCommand())
        {
            check.Transaction = tx; check.CommandText = "SELECT payload FROM documents WHERE kind='participation-session'";
            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                var s = JsonDefaults.Read<AssessmentSession>(protector.Unprotect(reader.GetString(0)));
                if (s.ConstructId == session.ConstructId && s.ConstructVersion == session.ConstructVersion && s.Test.Hash == session.Test.Hash)
                    throw new InvalidOperationException("This profile version already took this test. Select its sealed session to compare another model.");
            }
        }
        InsertDocument(c, tx, "participation-session", session.Id, session); tx.Commit(); return Task.CompletedTask;
    }
    public async Task<ParticipationRun> BeginParticipationRunAsync(string sessionId, AgentConnection connection, PersonaCondition condition)
    {
        connection.Validate();
        var mode = connection.Transport == AgentTransport.ExternalAgent ? "external-manual" : "api-model";
        using var c = Open(); using var tx = c.BeginTransaction();
        var s = ReadDocument<AssessmentSession>(c, tx, "participation-session", sessionId);
        var p = RequireParticipationProfile(c, tx, s);
        var context = ParticipationContext.Compile(p, condition);
        // Resume a frozen run rather than selecting a more favorable resample.
        using (var check = c.CreateCommand())
        {
            check.Transaction = tx; check.CommandText = "SELECT payload FROM documents WHERE kind='participation-run'";
            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                var r = JsonDefaults.Read<ParticipationRun>(protector.Unprotect(reader.GetString(0)));
                if (r.SessionId == s.Id && r.Connection.Identity == connection.Identity && r.Condition == condition && r.EvidenceKind == mode)
                    return r;
            }
        }
        var run = new ParticipationRun { SessionId = s.Id, Connection = connection, Condition = condition,
            ContextHash = Metrics.Hash(context), EvidenceKind = mode };
        InsertDocument(c, tx, "participation-run", run.Id, run); tx.Commit();
        return await Task.FromResult(run);
    }
    public Task<(AssessmentSession Session, ParticipationRun Run, string Context)> LoadParticipationTaskAsync(string runId)
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        var r = ReadDocument<ParticipationRun>(c, tx, "participation-run", runId);
        var s = ReadDocument<AssessmentSession>(c, tx, "participation-session", r.SessionId);
        var p = RequireParticipationProfile(c, tx, s);
        var context = ParticipationContext.Compile(p, r.Condition);
        if (Metrics.Hash(context) != r.ContextHash) throw new InvalidOperationException("Compiler context changed; this frozen run cannot resume.");
        return Task.FromResult((s, r, context));
    }
    public Task SaveParticipationResponseAsync(ParticipationResponse response)
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        var r = ReadDocument<ParticipationRun>(c, tx, "participation-run", response.RunId);
        var s = ReadDocument<AssessmentSession>(c, tx, "participation-session", r.SessionId);
        RequireParticipationProfile(c, tx, s); // Recheck after inference, in the same transaction as the write.
        ParticipationScoring.Score(s, r, [response]);
        InsertDocument(c, tx, "participation-response", $"{response.RunId}:{response.ItemId}", response);
        tx.Commit(); return Task.CompletedTask;
    }
    public async Task<AgentTaskPacket> ExportParticipationTaskAsync(string runId)
    {
        var (s, r, context) = await LoadParticipationTaskAsync(runId);
        if (r.Connection.Transport != AgentTransport.ExternalAgent) throw new InvalidOperationException("Select the external agent route for a manual packet.");
        return AgentTaskPacket.Create(s, r, context);
    }
    public async Task ImportAgentAnswersAsync(ExternalAnswerBatch batch)
    {
        var packet = await ExportParticipationTaskAsync(batch.RunId);
        if (batch.Schema != "hekasomi.agent-answers/1" || batch.PacketHash != packet.Hash || batch.Answers is null
            || batch.Answers.Count != packet.Items.Count || batch.Answers.Select(a => a.ItemId).Distinct().Count() != batch.Answers.Count)
            throw new InvalidDataException("Agent answers must match the exact packet hash and contain each question once. Use null for abstentions.");
        using var c = Open(); using var tx = c.BeginTransaction();
        var r = ReadDocument<ParticipationRun>(c, tx, "participation-run", batch.RunId);
        var s = ReadDocument<AssessmentSession>(c, tx, "participation-session", r.SessionId);
        RequireParticipationProfile(c, tx, s);
        var rows = batch.Answers.Select(a => new ParticipationResponse(r.Id, a.ItemId,
            a.Choice is null ? ResponseState.Abstained : ResponseState.Answered, a.Choice)).ToArray();
        ParticipationScoring.Score(s, r, rows);
        foreach (var row in rows) InsertDocument(c, tx, "participation-response", $"{row.RunId}:{row.ItemId}", row);
        tx.Commit();
    }
    public async Task<bool> ImportPlatformReportAsync(PlatformReport report, string origin)
    {
        report.Validate();
        if (origin != "file-unverified" && origin != "platform-https") throw new InvalidDataException("Unknown import origin.");
        var id = origin + ":" + Metrics.Hash(JsonDefaults.Write(report));
        var imports = await ListAsync<ImportedPlatformReport>("platform-report");
        if (imports.Any(i => i.Id == id)) return false;
        // A repeated report ID with changed contents is a conflict, never a silent replacement.
        if (imports.Any(i => i.Origin == origin && i.Report.Id == report.Id)) throw new InvalidDataException("Report ID already exists with different contents.");
        if (origin == "platform-https")
        {
            var previous = imports.Where(i => i.Origin == origin).ToArray();
            if (previous.Any(i => i.Report.ExportedAt > report.ExportedAt)) throw new InvalidDataException("The platform returned an older snapshot. Existing platform data was retained.");
            using var c = Open(); using var tx = c.BeginTransaction();
            foreach (var old in previous)
            {
                using var delete = c.CreateCommand(); delete.Transaction = tx;
                delete.CommandText = "DELETE FROM documents WHERE kind='platform-report' AND id=$id";
                delete.Parameters.AddWithValue("$id", old.Id); delete.ExecuteNonQuery();
            }
            InsertDocument(c, tx, "platform-report", id, new ImportedPlatformReport(id, origin, DateTimeOffset.UtcNow, report));
            tx.Commit(); return true;
        }
        await PutAsync("platform-report", id, "Imported", new ImportedPlatformReport(id, origin, DateTimeOffset.UtcNow, report), protectExisting: true);
        return true;
    }
    public Task DeletePlatformReportsAsync()
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM documents WHERE kind='platform-report'"; cmd.ExecuteNonQuery(); return Task.CompletedTask;
    }
}
