using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using Xunit;

namespace HekaSomi.Lab.Tests;

public sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hekasomi-tests-" + Guid.NewGuid().ToString("N"));
    public TemporaryDirectory() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(Path, true);
    }
}
public class MetricTests
{
    [Theory]
    [InlineData("one two three", "one too three", 1, 0, 0)]
    [InlineData("one two three", "one three", 0, 1, 0)]
    [InlineData("one two", "one two three", 0, 0, 1)]
    [InlineData("Hello, WORLD!", "hello world", 0, 0, 0)]
    [InlineData("ＡＢＣ café", "abc cafe\u0301", 0, 0, 0)]
    public void EditCounts(string reference, string hypothesis, int s, int d, int i)
    {
        var score = Metrics.Wer(reference, hypothesis);
        Assert.Equal((s, d, i), (score.Substitutions, score.Deletions, score.Insertions));
    }
    [Fact]
    public void EmptyReferenceIsUnscored()
    {
        Assert.Null(Metrics.Wer("", "hello").Rate);
        Assert.Equal(1, Metrics.Wer("", "hello").Insertions);
    }
    [Fact]
    public void InsertionRateCanExceedOne()
    {
        Assert.Equal(2d, Metrics.Wer("hello", "hello one two").Rate);
    }
    [Fact]
    public void CerSupportsUnsegmentedText()
    {
        Assert.Equal(1, Metrics.Cer("你好世界", "你好世").Deletions);
    }
    [Fact]
    public void PercentilesInterpolateAndEmptyIsMissing()
    {
        Assert.Equal(28.5, Metrics.Percentile([0, 10, 20, 30], .95)!.Value, 10);
        Assert.Null(Metrics.Percentile([], .5));
    }
    [Fact] public void BadPercentileRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => Metrics.Percentile([1, 2], 1.1));
}
public class ConstructTests
{
    public static PersonalityConstruct Construct() => new() { Name = "Test person", Status = ConstructStatus.Draft, Evidence = [new("source", "social", "Question", "I use concise language. Private memory: orchard.", DateTimeOffset.UtcNow)], Claims = [new() { Statement = "Prefers concise wording", SourceId = "source", Category = "Communication", Approved = true }, new() { Statement = "Values patience", SourceId = "source", Category = "Values", Approved = true }] };
    [Fact]
    public void InstrumentHas50UniqueItems()
    {
        Assert.Equal(50, IpipQuestionnaire.Items.Count);
        Assert.Equal(50, IpipQuestionnaire.Items.Select(i => i.Id).Distinct().Count());
        Assert.All(IpipQuestionnaire.Items.GroupBy(i => i.Domain), g => Assert.Equal(10, g.Count()));
    }
    [Fact]
    public void ReverseScoringUsesCorrectKey()
    {
        var a = IpipQuestionnaire.Items.ToDictionary(i => i.Id, i => i.Reversed ? 1 : 5);
        Assert.All(IpipQuestionnaire.Score(a), s => Assert.Equal(50, s.Total));
    }
    [Fact]
    public void NeutralAnswersScore30()
    {
        Assert.All(IpipQuestionnaire.Score(IpipQuestionnaire.Items.ToDictionary(i => i.Id, _ => 3)), s => Assert.Equal(30, s.Total));
    }
    [Fact]
    public void MissingResponsesNotImputed()
    {
        var scores = IpipQuestionnaire.Score(new Dictionary<int, int> { { 1, 5 } });
        Assert.Null(scores[0].Total);
        Assert.Equal(1, scores[0].Answered);
    }
    [Theory][InlineData(1, 0)][InlineData(1, 6)][InlineData(51, 3)] public void InvalidResponsesRejected(int id, int value) => Assert.Throws<InvalidDataException>(() => IpipQuestionnaire.Score(new Dictionary<int, int> { { id, value } }));
    [Fact]
    public void UnknownClaimSourcePreventsApproval()
    {
        var p = Construct();
        p.Claims[0].SourceId = "invented";
        Assert.Throws<InvalidOperationException>(() => ConstructCompiler.ValidateForApproval(p));
    }
    [Fact]
    public void IncompleteSurveyAloneCannotApprove()
    {
        var p = new PersonalityConstruct { Questionnaire = new() { { 1, 5 } } };
        Assert.Throws<InvalidOperationException>(() => ConstructCompiler.ValidateForApproval(p));
    }
    [Fact]
    public void TranslationExcludesPrivateStoriesValuesAndAnswers()
    {
        var p = Construct() with
        {
            Status = ConstructStatus.Approved
        };
        var text = ConstructCompiler.ForTranslation(p);
        Assert.Contains("Prefers concise", text);
        Assert.DoesNotContain("orchard", text);
        Assert.DoesNotContain("Values patience", text);
        Assert.DoesNotContain("responseData", text);
    }
    [Theory][InlineData(ConstructStatus.Draft)][InlineData(ConstructStatus.Revoked)] public void UnapprovedVersionsCannotCompile(ConstructStatus status) => Assert.Throws<InvalidOperationException>(() => ConstructCompiler.ForTranslation(Construct() with { Status = status }));
    [Fact]
    public void AblationsExcludeOtherInputs()
    {
        var p = Construct() with
        {
            Status = ConstructStatus.Approved,
            Questionnaire = new() { { 1, 5 } }
        };
        Assert.DoesNotContain("orchard", ConstructCompiler.ForPrediction(p, PersonaCondition.QuestionnaireOnly));
        Assert.Contains("orchard", ConstructCompiler.ForPrediction(p, PersonaCondition.InterviewOnly));
        Assert.DoesNotContain("Prefers concise", ConstructCompiler.ForPrediction(p, PersonaCondition.Neutral));
    }
    [Fact]
    public void RevisionDoesNotMutateOriginal()
    {
        var p = Construct();
        var copy = ConstructCompiler.NewRevision(p, 2);
        copy.Claims[0].Statement = "Changed";
        copy.Questionnaire[1] = 4;
        Assert.NotEqual(copy.Claims[0].Statement, p.Claims[0].Statement);
        Assert.Empty(p.Questionnaire);
        Assert.Equal(2, copy.Version);
    }
    [Fact]
    public void RepeatedPredictionsDoNotInflateN()
    {
        var now = DateTimeOffset.UtcNow;
        var p = new PersonaPrediction("case", "owner", "construct", 1, "hash", PersonaCondition.Neutral, "B", "model", now);
        var scores = BehavioralEvaluation.Summarize([p, p with { Choice = "A", CreatedAt = now.AddSeconds(1) }], new Dictionary<string, SealedAnswers> { { "case", new("case", "owner", "A", "B") } });
        Assert.Single(scores);
        Assert.Equal(1, scores[0].Cases);
        Assert.Equal(1, scores[0].AccuracyT0);
        Assert.Equal(0, scores[0].RetestAgreement);
        Assert.Equal(0, scores[0].AccuracyT1);
    }
    [Fact]
    public void OtherOwnerAnswersNeverScore()
    {
        var p = new PersonaPrediction("case", "alice", "c", 1, "h", PersonaCondition.Neutral, "A", "m", DateTimeOffset.UtcNow);
        Assert.Equal(0, BehavioralEvaluation.Summarize([p], new Dictionary<string, SealedAnswers> { { "case", new("case", "bob", "A") } })[0].Cases);
    }
}
public class RepositoryTests
{
    [Fact]
    public async Task OnlyApprovedVersionsCanBeRevoked()
    {
        using var dir = new TemporaryDirectory();
        var repo = Repo(dir.Path);
        var p = ConstructTests.Construct();
        await repo.SaveDraftAsync(p);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.RevokeAsync(p));
        Assert.Equal(ConstructStatus.Draft, (await repo.ListAsync<PersonalityConstruct>("construct"))[0].Status);
        await repo.ApproveAsync(p);
        await repo.RevokeAsync(p);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.RevokeAsync(p));
        Assert.Equal(ConstructStatus.Revoked, (await repo.ListAsync<PersonalityConstruct>("construct"))[0].Status);
    }

    private static LabRepository Repo(string path) => new(System.IO.Path.Combine(path, "lab.db"), new AesContentProtector(SHA256.HashData(Encoding.UTF8.GetBytes("test-only-key"))));
    [Fact]
    public async Task RoundtripEncryptsSensitivePayload()
    {
        using var dir = new TemporaryDirectory();
        var repo = Repo(dir.Path);
        var p = ConstructTests.Construct();
        await repo.SaveDraftAsync(p);
        Assert.Equal(p.Evidence[0].Text, (await repo.ListAsync<PersonalityConstruct>("construct"))[0].Evidence[0].Text);
        Assert.DoesNotContain("orchard", Encoding.UTF8.GetString(File.ReadAllBytes(System.IO.Path.Combine(dir.Path, "lab.db"))));
    }
    [Fact]
    public async Task ApprovedAndRevokedVersionsAreImmutable()
    {
        using var dir = new TemporaryDirectory();
        var repo = Repo(dir.Path);
        var p = ConstructTests.Construct();
        await repo.ApproveAsync(p);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveDraftAsync(p));
        await repo.RevokeAsync(p);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ApproveAsync(p));
        Assert.Equal(ConstructStatus.Revoked, (await repo.ListAsync<PersonalityConstruct>("construct"))[0].Status);
    }
    [Fact]
    public async Task AnswerKeySealsT0AndOneRetest()
    {
        using var dir = new TemporaryDirectory();
        var repo = Repo(dir.Path);
        await repo.SealCaseAsync(new("c", "o", "Question", ["A", "B"]), new("c", "o", "A"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveAnswersAsync(new("c", "o", "B")));
        await repo.SaveAnswersAsync(new("c", "o", "A", "B"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveAnswersAsync(new("c", "o", "A", "A")));
    }
    [Fact]
    public async Task SealingIsAtomic()
    {
        using var dir = new TemporaryDirectory();
        var repo = Repo(dir.Path);
        await Assert.ThrowsAsync<InvalidDataException>(() => repo.SealCaseAsync(new("c", "o", "Question", ["A", "B"]), new("c", "o", "C")));
        Assert.Empty(await repo.ListAsync<ValidationStimulus>("stimulus"));
        Assert.Empty(await repo.ListAsync<SealedAnswers>("answer"));
    }
    [Fact]
    public async Task PredictionCannotBeReplaced()
    {
        using var dir = new TemporaryDirectory();
        var repo = Repo(dir.Path);
        var p = new PersonaPrediction("c", "o", "p", 1, "hash", PersonaCondition.Neutral, "A", "m", DateTimeOffset.UtcNow);
        await repo.SavePredictionAsync(p);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SavePredictionAsync(p with { Choice = "B" }));
    }
    [Fact]
    public async Task DeleteCascadesOnlyLinkedRecords()
    {
        using var dir = new TemporaryDirectory();
        var repo = Repo(dir.Path);
        var p = ConstructTests.Construct();
        var unrelated = ConstructTests.Construct();
        await repo.ApproveAsync(p);
        await repo.ApproveAsync(unrelated);
        await repo.SealCaseAsync(new("c", p.OwnerId, "Question", ["A", "B"]), new("c", p.OwnerId, "A"));
        var run = new ComparisonRun { ConstructId = p.Id };
        await repo.SaveRunAsync(run);
        await repo.SaveReviewAsync(new(run.Id, "c", "Baseline", 3, null, false, ""));
        await repo.SavePredictionAsync(new("c", p.OwnerId, p.Id, 1, "hash", PersonaCondition.Neutral, "A", "m", DateTimeOffset.UtcNow));
        await repo.DeleteConstructAsync(p.Id);
        Assert.Single(await repo.ListAsync<PersonalityConstruct>("construct"));
        Assert.Empty(await repo.ListAsync<ComparisonRun>("run"));
        Assert.Empty(await repo.ListAsync<ReviewRating>("review"));
        Assert.Empty(await repo.ListAsync<PersonaPrediction>("prediction"));
        Assert.Empty(await repo.ListAsync<SealedAnswers>("answer"));
    }
    [Fact]
    public void CiphertextTamperingRejected()
    {
        var protector = new AesContentProtector(new byte[32]);
        var data = Convert.FromBase64String(protector.Protect("private"));
        data[^1] ^= 1;
        Assert.Throws<AuthenticationTagMismatchException>(() => protector.Unprotect(Convert.ToBase64String(data)));
    }
}
public class EngineTests
{
    private static Dataset Demo() => new() { Cases = [new() { Id = "a", Name = "Case", ReferenceTranscript = "do not cancel", BaselineReplay = new("do cancel", "cancelar"), CandidateReplay = new("do not cancel", "no cancelar") }] };
    [Fact]
    public async Task ReplayHasNoInventedTimings()
    {
        using var http = new HttpClient();
        var run = await new BenchmarkRunner(new TranslationEngine(http)).RunAsync(Demo(), new(), new());
        Assert.Equal(2, run.Results.Count);
        Assert.All(run.Results, r => Assert.Null(r.Output!.TotalMs));
        Assert.Equal(1, run.Results[0].WordScore!.Errors);
        Assert.Contains(run.Gates, g => g.Metric == "Translation meaning" && g.Status == GateStatus.Inconclusive);
    }
    private sealed class CancelAfterFirst(CancellationTokenSource cts) : ITranslationEngine
    {
        public Task<EngineOutput> ProcessAsync(AudioCase c, Dataset d, EngineConfiguration config, string arm, string? style, CancellationToken ct)
        {
            cts.Cancel();
            return Task.FromResult(new EngineOutput("do not cancel", "no cancelar", 1, 1, 2, "test"));
        }
    }
    [Fact]
    public async Task CancellationRetainsCompletedOutput()
    {
        using var cts = new CancellationTokenSource();
        var run = await new BenchmarkRunner(new CancelAfterFirst(cts)).RunAsync(Demo(), new(), new(), cancellationToken: cts.Token);
        Assert.Equal("Canceled", run.State);
        Assert.Single(run.Results);
        Assert.Contains(run.Gates, g => g.Metric == "Execution" && g.Status == GateStatus.Fail);
    }
    [Fact]
    public async Task PerCaseFailureIsVisible()
    {
        using var http = new HttpClient();
        var d = new Dataset { Cases = [new()] };
        var run = await new BenchmarkRunner(new TranslationEngine(http)).RunAsync(d, new(), new());
        Assert.Equal(2, run.Results.Count);
        Assert.All(run.Results, r => Assert.NotNull(r.Error));
        Assert.Contains(run.Gates, g => g.Status == GateStatus.Fail);
    }
    [Fact]
    public async Task AudioContentAffectsFingerprint()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "audio.wav");
        File.WriteAllBytes(path, [1, 2, 3]);
        var d = Demo() with
        {
            Cases = [Demo().Cases[0] with { AudioPath = path }]
        };
        using var http = new HttpClient();
        var runner = new BenchmarkRunner(new TranslationEngine(http));
        var a = await runner.RunAsync(d, new(), new());
        File.WriteAllBytes(path, [1, 2, 4]);
        var b = await runner.RunAsync(d, new(), new());
        Assert.NotEqual(a.DatasetHash, b.DatasetHash);
        Assert.NotEqual(a.AudioHashes["a"], b.AudioHashes["a"]);
    }
    [Fact]
    public async Task TextModeDoesNotClaimAsrAccuracy()
    {
        using var http = new HttpClient(new Handler("Hola"));
        var d = new Dataset { Cases = [new() { TextInput = "Hello", ReferenceTranscript = "Hello" }] };
        var cfg = new EngineConfiguration { Kind = EngineKind.TextAndOllama, Model = "m" };
        var run = await new BenchmarkRunner(new TranslationEngine(http)).RunAsync(d, cfg, cfg);
        Assert.All(run.Results, r => Assert.Null(r.WordScore));
        Assert.Contains(run.Gates, g => g.Metric.StartsWith("WER") && g.Status == GateStatus.Inconclusive);
    }
    [Fact]
    public async Task DuplicateCasesRejected()
    {
        using var http = new HttpClient();
        var d = Demo();
        d.Cases.Add(d.Cases[0]);
        await Assert.ThrowsAsync<InvalidDataException>(() => new BenchmarkRunner(new TranslationEngine(http)).RunAsync(d, new(), new()));
    }
    private sealed class Handler(string content) : HttpMessageHandler
    {
        public string? Request; protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonDefaults.Write(new
                {
                    message = new
                    {
                        content
                    }
                }))
            };
        }
    }
    [Fact]
    public async Task PredictionPromptHasNoAnswerKey()
    {
        var handler = new Handler("{\"choice\":\"B\"}");
        using var http = new HttpClient(handler);
        var client = new OllamaClient(http, "http://localhost:11434");
        var result = await client.PredictAsync(new("c", "owner", "Choose an activity", ["A", "B"]), "Approved context", "model", default);
        Assert.Equal("B", result);
        Assert.DoesNotContain("T0", handler.Request);
        Assert.DoesNotContain("T1", handler.Request);
        Assert.DoesNotContain("owner", JsonDocument.Parse(handler.Request!).RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
    }
    [Fact]
    public async Task InvalidPredictionRejected()
    {
        using var http = new HttpClient(new Handler("{\"choice\":\"C\"}"));
        await Assert.ThrowsAsync<InvalidDataException>(() => new OllamaClient(http, "http://localhost:11434").PredictAsync(new("c", "o", "Q", ["A", "B"]), "context", "model", default));
    }
    [Fact]
    public async Task LiveTextUsesInputNotReference()
    {
        var handler = new Handler("Hola");
        using var http = new HttpClient(handler);
        var sample = new AudioCase { TextInput = "Hello", ReferenceTranscript = "SECRET REFERENCE" };
        var result = await new TranslationEngine(http).ProcessAsync(sample, new(), new()
        {
            Kind = EngineKind.TextAndOllama,
            Model = "model"
        }, "Baseline", null, default);
        Assert.DoesNotContain("SECRET REFERENCE", handler.Request);
        Assert.Equal("Hello", result.Transcript);
        Assert.Null(result.AsrMs);
        Assert.NotNull(result.TotalMs);
    }
    [Fact]
    public async Task RemotePlainHttpRejected()
    {
        using var http = new HttpClient(new Handler("hi"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new OllamaClient(http, "http://example.com").ChatAsync("m", "s", new { }, default));
    }
    [Fact]
    public async Task HallucinatedEvidenceSourceRejected()
    {
        using var http = new HttpClient(new Handler("{\"claims\":[{\"statement\":\"Friendly\",\"category\":\"Communication\",\"context\":\"All\",\"sourceId\":\"invented\"}]}"));
        await Assert.ThrowsAsync<InvalidDataException>(() => new OllamaClient(http, "http://localhost:11434").SuggestClaimsAsync(ConstructTests.Construct(), "m", default));
    }
}
public class FileTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData(" +SUM(A1)")]
    [InlineData("@danger")]
    [InlineData("\tformula")]
    public void CsvNeutralizesSpreadsheetFormulas(string text) => Assert.StartsWith("\"'", RunReports.CsvCell(text));
    [Fact] public void HtmlEscapesUserContent() => Assert.DoesNotContain("<script>", RunReports.Html(new() { DatasetName = "<script>alert(1)</script>" }));
    [Fact]
    public async Task ReportWritesAllFormats()
    {
        using var dir = new TemporaryDirectory();
        await RunReports.ExportAsync(dir.Path, new());
        Assert.All(new[] { "run.json", "results.csv", "report.html" }, f => Assert.True(File.Exists(Path.Combine(dir.Path, f))));
    }
    [Fact]
    public async Task DatasetResolvesRelativeAudio()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "demo.json");
        await DatasetFiles.SaveAsync(path, new()
        {
            Cases = [new() { AudioPath = "sound.wav" }]
        });
        Assert.Equal(Path.Combine(dir.Path, "sound.wav"), (await DatasetFiles.LoadAsync(path)).Cases[0].AudioPath);
    }
    [Fact]
    public void InspectPcmDurationAndPeaks()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "sound.wav");
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + 32000);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(16000);
            writer.Write(32000);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(32000);
            for (var i = 0; i < 16000; i++)
                writer.Write((short)16384);
        }
        var w = WaveFile.Inspect(path);
        Assert.Equal(1, w.DurationSeconds);
        Assert.All(w.Peaks, p => Assert.Equal(.5, p));
        Assert.Equal(.5, w.Rms);
    }
    [Fact]
    public void InvalidWavRejected()
    {
        using var dir = new TemporaryDirectory();
        var p = Path.Combine(dir.Path, "bad.wav");
        File.WriteAllText(p, "not a WAV file");
        Assert.Throws<InvalidDataException>(() => WaveFile.Inspect(p));
    }
}
