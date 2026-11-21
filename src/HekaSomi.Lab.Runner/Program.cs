using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("HekaSomi Lab\ncompare --manifest samples/demo.json --baseline-config samples/baseline.json --candidate-config samples/candidate.json --output artifacts/run [--construct approved-profile.json] [--strict]\nparticipation-score --report platform-report.json --output artifacts/participation\nparticipation-fixtures --output samples/participation\nExit 0: descriptive automated checks pass; 1: failed comparison; 2: configuration or required evidence unavailable. --strict also requires all release evidence (currently inconclusive human gates).\nExports contain source text; choose a private output folder.");
    return 0;
}
try
{
    string Required(string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new ArgumentException($"Missing {option}.");
    }
    if (args[0] == "participation-fixtures")
    {
        var directory = Path.GetFullPath(Required("--output")); Directory.CreateDirectory(directory);
        var pilot = ParticipationPilot.Create();
        await File.WriteAllTextAsync(Path.Combine(directory, "pilot-test.json"), JsonDefaults.Write(pilot));
        await File.WriteAllTextAsync(Path.Combine(directory, "hash-fixture.json"), JsonDefaults.Write(new { test = pilot, expectedHash = pilot.Hash }));
        var s = new AssessmentSession { Id = "synthetic-session", OwnerId = "synthetic-owner", ConstructId = "synthetic-profile", ConstructVersion = 1, ConstructHash = "synthetic-only",
            Test = pilot, HumanAnswers = pilot.Items.ToDictionary(i => i.Id, i => i.Options[0]), HeldOutAttested = true };
        var r = new ParticipationRun { Id = "synthetic-run", SessionId = s.Id, Connection = new() { Provider = "synthetic", Model = "fixture", Transport = AgentTransport.ExternalAgent },
            ContextHash = Metrics.Hash("synthetic context"), EvidenceKind = "synthetic-demo" };
        var report = new PlatformReport { Id = "synthetic-report", Evaluations = [new(s, r, pilot.Items.Select((i, n) => new ParticipationResponse(r.Id, i.Id, ResponseState.Answered, i.Options[n % 2])).ToList())],
            Events = [new("synthetic-event", "synthetic-game", "joined", "agent", DateTimeOffset.UtcNow)] };
        report.Validate(); await File.WriteAllTextAsync(Path.Combine(directory, "synthetic-platform-report.json"), JsonDefaults.Write(report));
        Console.WriteLine($"Wrote a practice test and clearly synthetic fixture. Test hash: {pilot.Hash}"); return 0;
    }
    if (args[0] == "participation-score")
    {
        var path = Required("--report");
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("Report exceeds 8 MiB.");
        var report = JsonDefaults.Read<PlatformReport>(await File.ReadAllTextAsync(path)); report.Validate();
        var scores = report.Evaluations.Select(e => new { e.Run.Id, e.Run.Connection.Provider, e.Run.Connection.Model, e.Run.Condition, e.Run.EvidenceKind,
            testHash = e.Session.Test.Hash, score = ParticipationScoring.Score(e.Session, e.Run, e.Responses), provenance = "file-unverified" }).ToArray();
        var directory = Path.GetFullPath(Required("--output")); Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "scores.json"), JsonDefaults.Write(scores));
        Console.WriteLine($"Validated and recomputed {scores.Length} runs. No global average inferred from file imports."); return 0;
    }
    if (args[0] != "compare") throw new ArgumentException("Unknown command. Use --help.");
    var dataset = await DatasetFiles.LoadAsync(Required("--manifest"));
    var baseline = JsonDefaults.Read<EngineConfiguration>(await File.ReadAllTextAsync(Required("--baseline-config")));
    var candidate = JsonDefaults.Read<EngineConfiguration>(await File.ReadAllTextAsync(Required("--candidate-config")));
    var construct = args.Contains("--construct") ? JsonDefaults.Read<PersonalityConstruct>(await File.ReadAllTextAsync(Required("--construct"))) : null;
    using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
    var run = await new BenchmarkRunner(new TranslationEngine(http)).RunAsync(dataset, baseline, candidate, construct,
        new Progress<RunProgress>(p => Console.WriteLine($"{p.Completed}/{p.Total} {p.Message}")), cts.Token);
    await RunReports.ExportAsync(Required("--output"), run);
    foreach (var gate in run.Gates)
        Console.WriteLine($"{gate.Status}: {gate.Metric} — {gate.Detail}");
    if (run.Gates.Any(g => g.Status == GateStatus.Fail))
        return 1;
    if (args.Contains("--strict") && run.Gates.Any(g => g.Status == GateStatus.Inconclusive))
        return 2;
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e.Message); return 2; }
