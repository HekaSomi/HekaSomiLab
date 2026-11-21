using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Xunit;

namespace HekaSomi.Lab.Tests;

public class ParticipationTests
{
    private static ParticipationTest Test() => new() { Id = "test", Title = "Synthetic test", Items = [new("q1", "First?", ["A", "B"]), new("q2", "Second?", ["A", "B"])] };
    private static AssessmentSession Session() => new() { OwnerId = "owner", ConstructId = "persona", ConstructVersion = 1, ConstructHash = "hash", Test = Test(), HumanAnswers = new() { ["q1"] = "A", ["q2"] = "B" }, HeldOutAttested = true };
    private static AgentConnection Connection(AgentTransport mode = AgentTransport.Ollama) => new() { Model = "test-model", Transport = mode };
    private static ParticipationRun Run(AssessmentSession s) => new() { SessionId = s.Id, Connection = Connection() };
    private static List<ParticipationResponse> All(ParticipationRun r, bool correct = true) => [new(r.Id, "q1", ResponseState.Answered, correct ? "A" : "B"), new(r.Id, "q2", ResponseState.Answered, correct ? "B" : "A")];
    private static LabRepository Repo(TemporaryDirectory d) => new(Path.Combine(d.Path, "test.db"), new AesContentProtector(RandomNumberGenerator.GetBytes(32)));
    private static async Task<(PersonalityConstruct Profile, AssessmentSession Session)> Seed(LabRepository repo)
    {
        var p = ConstructTests.Construct(); await repo.SaveDraftAsync(p); await repo.ApproveAsync(p);
        p = (await repo.ListAsync<PersonalityConstruct>("construct")).Single();
        var s = Session() with { OwnerId = p.OwnerId, ConstructId = p.Id, ConstructVersion = p.Version, ConstructHash = Metrics.Hash(JsonDefaults.Write(p)) };
        await repo.SealParticipationAsync(s); return (p, s);
    }
    [Fact] public void MissingAndAbstainedAnswersStayInDenominator()
    {
        var s = Session(); var r = Run(s);
        var score = ParticipationScoring.Score(s, r, [new(r.Id, "q1", ResponseState.Answered, "A")]);
        Assert.Equal(50, score.MatchPercent); Assert.Equal(50, score.CoveragePercent); Assert.Equal(100, score.AnsweredMatchPercent); Assert.Equal(1, score.Missing);
        var abstain = ParticipationScoring.Score(s, r, [new(r.Id, "q1", ResponseState.Abstained, null), new(r.Id, "q2", ResponseState.Failed, null)]);
        Assert.Equal(0, abstain.MatchPercent); Assert.Null(abstain.AnsweredMatchPercent);
    }
    [Theory][InlineData("C")][InlineData("a")][InlineData("")]
    public void OnlyExactOptionsCanScore(string choice)
    {
        var s = Session(); var r = Run(s);
        Assert.Throws<InvalidDataException>(() => ParticipationScoring.Score(s, r, [new(r.Id, "q1", ResponseState.Answered, choice)]));
    }
    [Fact] public void DuplicateAndCrossRunResultsAreRejected()
    {
        var s = Session(); var r = Run(s); var item = All(r)[0];
        Assert.Throws<InvalidDataException>(() => ParticipationScoring.Score(s, r, [item, item]));
        Assert.Throws<InvalidDataException>(() => ParticipationScoring.Score(s, r, [item with { RunId = "other" }]));
        Assert.Throws<InvalidDataException>(() => ParticipationScoring.Score(s, r, [item with { State = (ResponseState)999 }]));
    }
    [Fact] public void IncompleteHumanAnswersAndMalformedTestAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => (Session() with { HumanAnswers = new() }).Validate());
        Assert.Throws<InvalidDataException>(() => (Test() with { Items = [Test().Items[0], Test().Items[0]] }).Validate());
        Assert.Throws<InvalidDataException>(() => (Test() with { Schema = "future/2" }).Validate());
    }
    [Fact] public void ContextContainsNoRawStoriesOrItemAnswers()
    {
        var p = ConstructTests.Construct() with { Status = ConstructStatus.Approved, Questionnaire = IpipQuestionnaire.Items.ToDictionary(i => i.Id, _ => 3) };
        var context = ParticipationContext.Compile(p, PersonaCondition.Combined);
        Assert.DoesNotContain("orchard", context); Assert.DoesNotContain("responses", context); Assert.DoesNotContain("AudioPath", context);
        Assert.Contains("Prefers concise wording", context); Assert.Contains("30", context);
        Assert.DoesNotContain("Prefers concise wording", ParticipationContext.Compile(p, PersonaCondition.Neutral));
    }
    [Fact] public void PacketDoesNotSerializeAnswersOwnerIdentityOrCredentials()
    {
        var s = Session(); var r = Run(s) with { ContextHash = Metrics.Hash("safe") };
        var json = JsonDefaults.Write(AgentTaskPacket.Create(s, r, "safe"));
        Assert.DoesNotContain("humanAnswers", json); Assert.DoesNotContain("ownerId", json); Assert.DoesNotContain("credential", json); Assert.Contains("First?", json);
    }
    [Theory][InlineData("http://example.com")][InlineData("https://key@example.com")][InlineData("https://example.com?api_key=secret")][InlineData("file:///tmp/socket")]
    public void UnsafeConnectionUrlsAreRejected(string url) => Assert.Throws<InvalidDataException>(() => (Connection() with { BaseUrl = url }).Validate());
    [Fact] public async Task SessionAndRunCannotBeResampledSilently()
    {
        using var d = new TemporaryDirectory(); var repo = Repo(d); var (_, s) = await Seed(repo);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SealParticipationAsync(s with { Id = Guid.NewGuid().ToString("N") }));
        var a = await repo.BeginParticipationRunAsync(s.Id, Connection(), PersonaCondition.Combined);
        var b = await repo.BeginParticipationRunAsync(s.Id, Connection(), PersonaCondition.Combined);
        Assert.Equal(a.Id, b.Id);
        await repo.SaveParticipationResponseAsync(All(a)[0]);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => repo.SaveParticipationResponseAsync(All(a)[0] with { Choice = "B" }));
    }
    [Fact] public async Task RevocationBlocksLateResultsAndNewRuns()
    {
        using var d = new TemporaryDirectory(); var repo = Repo(d); var (p, s) = await Seed(repo);
        var r = await repo.BeginParticipationRunAsync(s.Id, Connection(), PersonaCondition.Combined);
        await repo.RevokeAsync(p);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.SaveParticipationResponseAsync(All(r)[0]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.BeginParticipationRunAsync(s.Id, Connection(), PersonaCondition.Neutral));
    }
    [Fact] public async Task ExternalPacketMustMatchAndImportIsAtomic()
    {
        using var d = new TemporaryDirectory(); var repo = Repo(d); var (_, s) = await Seed(repo);
        var r = await repo.BeginParticipationRunAsync(s.Id, Connection(AgentTransport.ExternalAgent), PersonaCondition.Combined);
        var packet = await repo.ExportParticipationTaskAsync(r.Id);
        var batch = new ExternalAnswerBatch("hekasomi.agent-answers/1", r.Id, packet.Hash, [new("q1", "A"), new("q2", "B")]);
        await Assert.ThrowsAsync<InvalidDataException>(() => repo.ImportAgentAnswersAsync(batch with { PacketHash = "wrong" }));
        await Assert.ThrowsAsync<InvalidDataException>(() => repo.ImportAgentAnswersAsync(batch with { Answers = [new("q1", "A"), new("q2", "invalid")] }));
        Assert.Empty(await repo.ListAsync<ParticipationResponse>("participation-response"));
        await repo.ImportAgentAnswersAsync(batch);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => repo.ImportAgentAnswersAsync(batch));
        Assert.Equal(2, (await repo.ListAsync<ParticipationResponse>("participation-response")).Count);
    }
    [Fact] public async Task DeletionRemovesNewAssessmentData()
    {
        using var d = new TemporaryDirectory(); var repo = Repo(d); var (p, s) = await Seed(repo);
        var r = await repo.BeginParticipationRunAsync(s.Id, Connection(), PersonaCondition.Combined); await repo.SaveParticipationResponseAsync(All(r)[0]);
        await repo.DeleteConstructAsync(p.Id);
        Assert.Empty(await repo.ListAsync<AssessmentSession>("participation-session"));
        Assert.Empty(await repo.ListAsync<ParticipationRun>("participation-run"));
        Assert.Empty(await repo.ListAsync<ParticipationResponse>("participation-response"));
    }
    [Fact] public async Task ImportsAreDeduplicatedAndChangedReportIdIsRejected()
    {
        using var d = new TemporaryDirectory(); var repo = Repo(d); var s = Session(); var r = Run(s);
        var report = new PlatformReport { Evaluations = [new(s, r, All(r))] };
        Assert.True(await repo.ImportPlatformReportAsync(report, "file-unverified")); Assert.False(await repo.ImportPlatformReportAsync(report, "file-unverified"));
        await Assert.ThrowsAsync<InvalidDataException>(() => repo.ImportPlatformReportAsync(report with { Events = [new("e", "game", "joined", "agent", DateTimeOffset.UtcNow)] }, "file-unverified"));
        var persisted = (await repo.ListAsync<ImportedPlatformReport>("platform-report")).Single(); Assert.Equal("file-unverified", persisted.Origin);
        await repo.DeletePlatformReportsAsync(); Assert.Empty(await repo.ListAsync<ImportedPlatformReport>("platform-report"));
    }
    [Fact] public void GlobalAverageWeightsOwnersOnceAndExcludesWithdrawnConsent()
    {
        var s = Session(); var r = Run(s); var contributions = new List<CohortContribution>();
        for (var i = 0; i < 10; i++) contributions.Add(new($"owner-{i}", new(s, r, All(r, i < 5)), true, false));
        // Extra attempts by one owner cannot overwhelm the cohort.
        contributions.AddRange(Enumerable.Range(0, 30).Select(_ => new CohortContribution("owner-0", new(s, r with { CreatedAt = r.CreatedAt.AddMinutes(1) }, All(r)), true, false)));
        contributions.Add(new("revoked", new(s, r, All(r)), true, true)); contributions.Add(new("no-consent", new(s, r, All(r)), false, false));
        var g = ParticipationCohorts.Build(s, r, contributions, DateTimeOffset.UtcNow);
        Assert.Equal(10, g.DistinctOwners); Assert.Equal(50, g.MeanMatchPercent); g.ValidateFor(s, r);
    }
    [Fact] public void SmallCohortIsSuppressedAndMismatchedGlobalAverageRejected()
    {
        var s = Session(); var r = Run(s);
        var g = ParticipationCohorts.Build(s, r, [new("owner", new(s, r, All(r)), true, false)], DateTimeOffset.UtcNow);
        Assert.True(g.Suppressed); Assert.Null(g.MeanMatchPercent); g.ValidateFor(s, r);
        Assert.Throws<InvalidDataException>(() => (g with { TestHash = "different" }).ValidateFor(s, r));
        Assert.Throws<InvalidDataException>(() => (g with { MeanMatchPercent = 50 }).ValidateFor(s, r));
    }
    [Fact] public void ModelAgreementUsesOnlyJointlyAnsweredCases()
    {
        var s = Session(); var a = Run(s); var b = Run(s);
        Assert.Equal(100, ParticipationScoring.PairAgreement(All(a), [new(b.Id, "q1", ResponseState.Answered, "A")]));
        Assert.Null(ParticipationScoring.PairAgreement(All(a), []));
    }
    private sealed class StubPredictor(Func<TestItem, CancellationToken, Task<AgentReply>> f) : IParticipationPredictor
    {
        public Task<AgentReply> PredictAsync(TestItem i, string context, AgentConnection c, string? key, CancellationToken ct) => f(i, ct);
    }
    [Fact] public async Task CancellationRetainsCompletedResultsAndResumeFillsOnlyMissing()
    {
        using var d = new TemporaryDirectory(); var repo = Repo(d); var (_, s) = await Seed(repo);
        var run = await repo.BeginParticipationRunAsync(s.Id, Connection(), PersonaCondition.Combined); using var cts = new CancellationTokenSource();
        var predictor = new StubPredictor((i, _) => { if (i.Id == "q2") { cts.Cancel(); cts.Token.ThrowIfCancellationRequested(); } return Task.FromResult(new AgentReply("A", "actual")); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ParticipationRunner(repo, predictor).RunAsync(run.Id, null, null, cts.Token));
        Assert.Single(await repo.ListAsync<ParticipationResponse>("participation-response"));
        var count = 0;
        await new ParticipationRunner(repo, new StubPredictor((_, _) => { count++; return Task.FromResult(new AgentReply("B", "actual")); })).RunAsync(run.Id, null, null, CancellationToken.None);
        Assert.Equal(1, count); Assert.Equal(2, (await repo.ListAsync<ParticipationResponse>("participation-response")).Count);
    }
    [Fact] public async Task ProviderErrorsBecomeScoredFailuresWithoutLeakingBodies()
    {
        using var d = new TemporaryDirectory(); var repo = Repo(d); var (_, s) = await Seed(repo);
        var run = await repo.BeginParticipationRunAsync(s.Id, Connection(), PersonaCondition.Combined);
        await new ParticipationRunner(repo, new StubPredictor((_, _) => throw new HttpRequestException("secret response body", null, HttpStatusCode.Unauthorized))).RunAsync(run.Id, null, null, CancellationToken.None);
        var result = Assert.Single(await repo.ListAsync<ParticipationResponse>("participation-response")); Assert.Equal("http-401", result.ErrorCode);
        Assert.DoesNotContain("secret", JsonDefaults.Write(result)); Assert.Equal(0, ParticipationScoring.Score(s, run, [result]).MatchPercent);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> f) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => f(request);
    }
    [Theory]
    [InlineData(AgentTransport.Responses, "responses", "{\"status\":\"completed\",\"model\":\"actual\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"{\\\"choice\\\":\\\"A\\\"}\"}]}]}")]
    [InlineData(AgentTransport.ChatCompletions, "chat/completions", "{\"model\":\"actual\",\"choices\":[{\"message\":{\"content\":\"{\\\"choice\\\":\\\"A\\\"}\"}}]}")]
    [InlineData(AgentTransport.AnthropicMessages, "messages", "{\"model\":\"actual\",\"content\":[{\"type\":\"text\",\"text\":\"{\\\"choice\\\":\\\"A\\\"}\"}]}")]
    [InlineData(AgentTransport.Ollama, "api/chat", "{\"model\":\"actual\",\"message\":{\"content\":\"{\\\"choice\\\":\\\"A\\\"}\"}}")]
    public async Task ProviderProtocolsUseExpectedPathsAndNeverSendAnswerKeys(AgentTransport transport, string path, string response)
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.EndsWith("/" + path, request.RequestUri!.AbsoluteUri);
            var payload = await request.Content!.ReadAsStringAsync(); Assert.DoesNotContain("humanAnswers", payload); Assert.DoesNotContain("SECRET", payload);
            if (transport == AgentTransport.AnthropicMessages) Assert.Equal("SECRET", request.Headers.GetValues("x-api-key").Single());
            else Assert.Equal("SECRET", request.Headers.Authorization!.Parameter);
            return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }));
        var answer = await new HttpParticipationPredictor(http).PredictAsync(Test().Items[0], "reviewed claims", Connection(transport), "SECRET", CancellationToken.None);
        Assert.Equal("A", answer.Choice); Assert.Equal("actual", answer.ActualModel);
    }
    [Fact] public async Task PlatformClientEncodesFiltersAndRejectsWrongCohort()
    {
        var s = Session(); var r = Run(s) with { Connection = Connection() with { Model = "model / revision" } };
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal("read-token", request.Headers.Authorization!.Parameter); Assert.Contains("model%20%2F%20revision", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonDefaults.Write(new GlobalCohort { TestHash = "wrong" })) });
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new PlatformClient(http, "https://platform.example", "read-token").FetchCohortAsync(s, r, CancellationToken.None));
    }
    [Fact] public async Task OversizedBodyIsRejected()
    {
        using var content = new StringContent(new string('a', 20));
        await Assert.ThrowsAsync<InvalidDataException>(() => BoundedJson.ReadAsync(content, 10, CancellationToken.None));
    }
    [Fact] public void OriginalPracticeInstrumentIsValid() => ParticipationPilot.Create().Validate();
    [Fact] public void TestHashMatchesIndependentPythonFixture()
    {
        Assert.Equal("1b14f4f45ba0c1383b0ed86f0265d790daf77d88a1413115b498cd220e2b1ba2", Test().Hash);
        Assert.Equal(Test().Hash, JsonDefaults.Read<ParticipationTest>(JsonDefaults.Write(Test())).Hash);
    }
    [Fact] public async Task AuthoritativeSnapshotReplacesPriorOnlineDataButRetainsFileImports()
    {
        using var d = new TemporaryDirectory(); var repo = Repo(d);
        var a = new PlatformReport { Id = "first", Events = [new("event", "game", "joined", "agent", DateTimeOffset.UtcNow)] };
        await repo.ImportPlatformReportAsync(a, "file-unverified"); await repo.ImportPlatformReportAsync(a, "platform-https");
        var b = new PlatformReport { Id = "second", ExportedAt = a.ExportedAt.AddSeconds(1) };
        await repo.ImportPlatformReportAsync(b, "platform-https");
        var reports = await repo.ListAsync<ImportedPlatformReport>("platform-report");
        Assert.Equal(2, reports.Count); Assert.Empty(reports.Single(r => r.Origin == "platform-https").Report.Events);
        Assert.Single(reports.Single(r => r.Origin == "file-unverified").Report.Events);
    }
}
