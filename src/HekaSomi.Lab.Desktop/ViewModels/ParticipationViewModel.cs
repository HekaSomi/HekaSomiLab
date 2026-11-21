using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using HekaSomi.Lab.Desktop.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace HekaSomi.Lab.Desktop.ViewModels;

public partial class ParticipationAnswerRow(TestItem item) : ObservableObject
{
    public TestItem Item { get; } = item;
    [ObservableProperty] private string? selectedAnswer;
}
public sealed record AssessmentRow(AssessmentSession Session)
{
    public string Label => $"{Session.Test.Title} · v{Session.ConstructVersion} · {Session.SealedAt.LocalDateTime:g} · {Session.Id[..8]}";
}
public sealed record QuestionComparisonRow(string Question, string YourChoice, string AgentChoice, string State, string Match);
public sealed record ParticipationDashboardRow(EvaluationObservation Observation, string Origin)
{
    private ParticipationScore Score => ParticipationScoring.Score(Observation.Session, Observation.Run, Observation.Responses);
    public string RunId => Observation.Run.Id;
    public string Provider => Observation.Run.Connection.Provider;
    public string Model => Observation.Run.Connection.Model;
    public string Condition => Observation.Run.Condition.ToString();
    public string Test => Observation.Session.Test.Title;
    public string Match => $"{Score.MatchPercent:F1}% ({Score.Matches}/{Score.Eligible})";
    public string Coverage => $"{Score.CoveragePercent:F1}%";
    public string Progress => $"{Score.Completed}/{Score.Eligible}";
    public string Mode => Observation.Run.EvidenceKind;
}

public partial class ParticipationViewModel(LabRepository repository, HttpClient http) : PageViewModel
{
    private ParticipationTest test = ParticipationPilot.Create();
    public ObservableCollection<PersonalityConstruct> Constructs { get; } = [];
    public ObservableCollection<ParticipationAnswerRow> Questions { get; } = [];
    public ObservableCollection<AssessmentRow> Sessions { get; } = [];
    public ObservableCollection<ParticipationDashboardRow> Dashboard { get; } = [];
    public ObservableCollection<QuestionComparisonRow> QuestionResults { get; } = [];
    public ObservableCollection<GameEvent> GameEvents { get; } = [];
    public IReadOnlyList<ProviderPreset> Providers => AgentProviders.Presets;
    public PersonaCondition[] Conditions => Enum.GetValues<PersonaCondition>();
    public AgentTransport[] Transports => Enum.GetValues<AgentTransport>();
    [ObservableProperty] private PersonalityConstruct? selectedConstruct;
    [ObservableProperty] private AssessmentRow? selectedSession;
    [ObservableProperty] private ParticipationDashboardRow? selectedResult;
    [ObservableProperty] private ProviderPreset selectedProvider = AgentProviders.Presets[5];
    [ObservableProperty] private AgentTransport transport = AgentTransport.Ollama;
    [ObservableProperty] private string baseUrl = "http://localhost:11434";
    [ObservableProperty] private string credentialVariable = "";
    [ObservableProperty] private string model = "";
    [ObservableProperty] private PersonaCondition condition = PersonaCondition.Combined;
    [ObservableProperty] private bool heldOutAttested;
    [ObservableProperty] private bool sharingConfirmed;
    [ObservableProperty] private string testTitle = "Everyday choices · 12-question practice test";
    [ObservableProperty] private string preview = "Select an approved profile and preview the context before sending it to an agent.";
    [ObservableProperty] private string globalAverage = "No global average loaded. Connect to the platform after its Lab endpoints are implemented.";
    [ObservableProperty] private string platformUrl = "https://hekasomi.ai";
    [ObservableProperty] private bool platformConfirmed;
    [ObservableProperty] private string eventSummary = "No platform events imported.";
    [ObservableProperty] private string comparison = "Select a result to inspect its comparison with other runs on the same sealed test.";
    [ObservableProperty] private int timeoutSeconds = 90;
    [ObservableProperty] private int maxOutputTokens = 2048;

    partial void OnSelectedProviderChanged(ProviderPreset value)
    {
        Transport = value.Transport; BaseUrl = value.BaseUrl; CredentialVariable = value.CredentialVariable; Model = ""; SharingConfirmed = false;
    }
    partial void OnBaseUrlChanged(string value) => SharingConfirmed = false;
    partial void OnTransportChanged(AgentTransport value) => SharingConfirmed = false;
    partial void OnModelChanged(string value) => SharingConfirmed = false;
    partial void OnConditionChanged(PersonaCondition value) { SharingConfirmed = false; Preview = "Preview the selected condition before sharing."; }
    partial void OnSelectedConstructChanged(PersonalityConstruct? value) { SharingConfirmed = false; SelectedSession = null; foreach (var q in Questions) q.SelectedAnswer = null; }
    partial void OnSelectedSessionChanged(AssessmentRow? value) { SharingConfirmed = false; Preview = "Preview this sealed session's profile before sharing."; }
    partial void OnPlatformUrlChanged(string value) { PlatformConfirmed = false; GlobalAverage = "Platform address changed; refresh to load its data."; }
    partial void OnSelectedResultChanged(ParticipationDashboardRow? value)
    {
        GlobalAverage = "No global average loaded for this result.";
        QuestionResults.Clear();
        if (value is null) { Comparison = "Select a result."; return; }
        var s = value.Observation.Session;
        foreach (var q in s.Test.Items)
        {
            var response = value.Observation.Responses.SingleOrDefault(r => r.ItemId == q.Id);
            QuestionResults.Add(new(q.Question, s.HumanAnswers[q.Id], response?.Choice ?? "—", response?.State.ToString() ?? "Missing",
                response?.State == ResponseState.Answered && response.Choice == s.HumanAnswers[q.Id] ? "Yes" : "No"));
        }
        var peers = Dashboard.Where(x => x.RunId != value.RunId && x.Origin == value.Origin && x.Observation.Session.Id == s.Id).ToArray();
        Comparison = peers.Length == 0 ? "No other model or condition has run on this sealed test yet."
            : string.Join("\n", peers.Select(p =>
            {
                var a = value.Observation.Responses.Where(r => r.State == ResponseState.Answered).Select(r => r.ItemId).ToHashSet();
                var shared = p.Observation.Responses.Count(r => r.State == ResponseState.Answered && a.Contains(r.ItemId));
                var agreement = ParticipationScoring.PairAgreement(value.Observation.Responses, p.Observation.Responses);
                return $"vs {p.Provider} / {p.Model} / {p.Condition}: {(agreement is null ? "unscored" : $"{agreement:F1}% agreement")} on {shared} jointly answered questions.";
            }));
    }
    public Task PrepareForAsync(PersonalityConstruct p) => Guard(async _ =>
    {
        await RefreshInternal(); SelectedConstruct = Constructs.FirstOrDefault(x => x.Key == p.Key); LoadTest(ParticipationPilot.Create());
        Message = "Your profile is ready. Take an optional test to see where its predictions agree with your choices.";
    });
    [RelayCommand] private Task Refresh() => Guard(_ => RefreshInternal());
    private async Task RefreshInternal()
    {
        var key = SelectedConstruct?.Key; var sessionId = SelectedSession?.Session.Id; var resultId = SelectedResult?.RunId;
        var draftAnswers = Questions.ToDictionary(q => q.Item.Id, q => q.SelectedAnswer);
        Constructs.Clear(); foreach (var p in await repository.ListAsync<PersonalityConstruct>("construct")) if (p.Status == ConstructStatus.Approved) Constructs.Add(p);
        SelectedConstruct = Constructs.FirstOrDefault(p => p.Key == key);
        if (SelectedConstruct is not null && SelectedConstruct.Key == key)
            foreach (var q in Questions) if (draftAnswers.TryGetValue(q.Item.Id, out var answer)) q.SelectedAnswer = answer;
        Sessions.Clear();
        var sessions = await repository.ListAsync<AssessmentSession>("participation-session");
        foreach (var s in sessions) Sessions.Add(new(s));
        SelectedSession = Sessions.FirstOrDefault(s => s.Session.Id == sessionId);
        var responses = await repository.ListAsync<ParticipationResponse>("participation-response");
        Dashboard.Clear();
        foreach (var r in await repository.ListAsync<ParticipationRun>("participation-run"))
        {
            var s = sessions.SingleOrDefault(s => s.Id == r.SessionId);
            if (s is not null) Dashboard.Add(new(new(s, r, responses.Where(x => x.RunId == r.Id).ToList()), "This computer"));
        }
        var reports = await repository.ListAsync<ImportedPlatformReport>("platform-report");
        var seen = new HashSet<string>();
        foreach (var report in reports)
            foreach (var e in report.Report.Evaluations)
                if (seen.Add(report.Origin + ":" + e.Run.Id)) Dashboard.Add(new(e, report.Origin == "platform-https" ? "Platform HTTPS" : "Imported file · unverified"));
        var events = reports.SelectMany(r => r.Report.Events).DistinctBy(e => e.Id).ToArray();
        GameEvents.Clear(); foreach (var e in events.OrderByDescending(e => e.OccurredAt)) GameEvents.Add(e);
        EventSummary = $"{events.Length} platform events · {events.Count(e => e.Kind == "actionAccepted")} accepted actions · {events.Count(e => e.Kind == "actionRejected")} rejected · {events.Count(e => e.Kind == "turnTimedOut")} timed out. Imported events are descriptive; file provenance is unverified.";
        SelectedResult = Dashboard.FirstOrDefault(x => x.RunId == resultId);
        if (Questions.Count == 0) LoadTest(test);
    }
    private void LoadTest(ParticipationTest definition)
    {
        definition.Validate(); test = definition; TestTitle = test.Title; Questions.Clear();
        foreach (var q in test.Items) Questions.Add(new(q)); HeldOutAttested = false;
    }
    [RelayCommand] private void LoadPilot() => LoadTest(ParticipationPilot.Create());
    private static async Task<T> ReadFile<T>(string path)
    {
        if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("File must be no larger than 8 MiB.");
        return JsonDefaults.Read<T>(await File.ReadAllTextAsync(path));
    }
    [RelayCommand] private Task ImportTest() => Guard(async _ =>
    {
        var path = Workspace.OpenFile("Participation test JSON|*.json"); if (path is not null) LoadTest(await ReadFile<ParticipationTest>(path));
    });
    [RelayCommand] private Task Seal() => Guard(async _ =>
    {
        var p = (await repository.ListAsync<PersonalityConstruct>("construct")).SingleOrDefault(p => p.Key == SelectedConstruct?.Key && p.Status == ConstructStatus.Approved)
            ?? throw new InvalidOperationException("Choose a currently approved profile.");
        var s = new AssessmentSession { OwnerId = p.OwnerId, ConstructId = p.Id, ConstructVersion = p.Version, ConstructHash = Metrics.Hash(JsonDefaults.Write(p)),
            Test = test, HumanAnswers = Questions.Where(q => q.SelectedAnswer is not null).ToDictionary(q => q.Item.Id, q => q.SelectedAnswer!), HeldOutAttested = HeldOutAttested };
        await repository.SealParticipationAsync(s); await RefreshInternal(); SelectedSession = Sessions.Single(x => x.Session.Id == s.Id);
        foreach (var q in Questions) q.SelectedAnswer = null;
        Message = "Your answers are sealed. You can now compare several models against this same test.";
    });
    private AgentConnection Connection() => new() { Provider = SelectedProvider.Id, Model = Model.Trim(), BaseUrl = BaseUrl.Trim(), Transport = Transport,
        CredentialEnvironmentVariable = CredentialVariable.Trim(), TimeoutSeconds = TimeoutSeconds, MaxOutputTokens = MaxOutputTokens };
    private async Task<string> Context()
    {
        var s = SelectedSession?.Session ?? throw new InvalidOperationException("Select a sealed test session first.");
        var p = (await repository.ListAsync<PersonalityConstruct>("construct")).SingleOrDefault(p => p.Id == s.ConstructId && p.Version == s.ConstructVersion && p.Status == ConstructStatus.Approved)
            ?? throw new InvalidOperationException("This test needs its currently approved profile.");
        return ParticipationContext.Compile(p, Condition);
    }
    [RelayCommand] private Task PreviewContext() => Guard(async _ => { Preview = await Context(); Message = "Only this profile context and the test questions go to the selected agent. Your sealed answers stay local."; });
    private async Task<ParticipationRun> Begin()
    {
        var s = SelectedSession?.Session ?? throw new InvalidOperationException("Take the test or select a sealed session.");
        var c = Connection(); c.Validate();
        if (!SharingConfirmed) throw new InvalidOperationException("Preview the profile context, then confirm sharing it with the selected agent.");
        if (Preview != await Context()) throw new InvalidOperationException("Preview this profile condition before starting or exporting its comparison.");
        return await repository.BeginParticipationRunAsync(s.Id, c, Condition);
    }
    [RelayCommand] private Task Compare() => Guard(async ct =>
    {
        if (Transport == AgentTransport.ExternalAgent) throw new InvalidOperationException("Use Export agent task, then Import agent answers for this route.");
        var c = Connection(); c.Validate();
        var credential = string.IsNullOrWhiteSpace(c.CredentialEnvironmentVariable) ? null : Environment.GetEnvironmentVariable(c.CredentialEnvironmentVariable);
        if (c.Transport != AgentTransport.Ollama && string.IsNullOrWhiteSpace(credential)) throw new InvalidOperationException($"Set {c.CredentialEnvironmentVariable} to your developer API key, then restart Lab. The key is never written into reports.");
        var run = await Begin();
        try { await new ParticipationRunner(repository, new HttpParticipationPredictor(http)).RunAsync(run.Id, credential, new Progress<string>(m => Message = m), ct); }
        finally { await RefreshInternal(); SelectedResult = Dashboard.FirstOrDefault(x => x.RunId == run.Id); }
        Message = "Comparison saved. Missing, invalid and failed answers remain in the primary score's denominator. Check coverage and completion.";
    });
    [RelayCommand] private Task ExportTask() => Guard(async _ =>
    {
        if (Transport != AgentTransport.ExternalAgent) throw new InvalidOperationException("Choose Any personal agent · manual first.");
        var run = await Begin(); var packet = await repository.ExportParticipationTaskAsync(run.Id);
        var path = Workspace.SaveFile("Agent task JSON|*.json", "hekasomi-agent-task.json");
        if (path is not null) { await File.WriteAllTextAsync(path, JsonDefaults.Write(new { packetHash = packet.Hash, packet })); Message = "Task exported without your test answers. Give this file to your agent and request the answer JSON described inside."; }
        await RefreshInternal();
    });
    [RelayCommand] private Task ImportAnswers() => Guard(async _ =>
    {
        var path = Workspace.OpenFile("Agent answer JSON|*.json"); if (path is null) return;
        await repository.ImportAgentAnswersAsync(await ReadFile<ExternalAnswerBatch>(path)); await RefreshInternal(); Message = "Agent answers imported as unverified manual results. A second import cannot replace frozen answers.";
    });
    [RelayCommand] private Task ImportReport() => Guard(async _ =>
    {
        var path = Workspace.OpenFile("Platform report JSON|*.json"); if (path is null) return;
        var added = await repository.ImportPlatformReportAsync(await ReadFile<PlatformReport>(path), "file-unverified");
        await RefreshInternal(); Message = added ? "Platform report imported. Scores were recomputed; file imports do not set the global average." : "This report was already imported.";
    });
    private PlatformClient Platform()
    {
        if (!PlatformConfirmed) throw new InvalidOperationException("Confirm the platform address before sending its read-only access token.");
        return new(http, PlatformUrl.Trim(), Environment.GetEnvironmentVariable("HEKASOMI_LAB_TOKEN") ?? "");
    }
    [RelayCommand] private Task FetchReport() => Guard(async ct =>
    {
        var report = await Platform().FetchReportAsync(ct); await repository.ImportPlatformReportAsync(report, "platform-https");
        await RefreshInternal(); Message = "Fetched authorized platform evaluations and game events.";
    });
    [RelayCommand] private Task FetchGlobal() => Guard(async ct =>
    {
        GlobalAverage = "No global average loaded.";
        var e = SelectedResult?.Observation ?? throw new InvalidOperationException("Select a result to choose its matching cohort.");
        var g = await Platform().FetchCohortAsync(e.Session, e.Run, ct);
        GlobalAverage = g.Suppressed ? $"Global average withheld for this cohort (minimum {g.MinimumOwners} owners). As of {g.AsOf.LocalDateTime:g}."
            : $"Platform-reported global average: {g.MeanMatchPercent:F1}% · coverage {g.MeanCoveragePercent:F1}% · {g.DistinctOwners} owners · as of {g.AsOf.LocalDateTime:g}. Each owner has equal weight.";
    });
    [RelayCommand] private Task ExportReport() => Guard(async _ =>
    {
        if (SelectedResult is null) throw new InvalidOperationException("Select a result.");
        if (MessageBox.Show("This readable report includes the test questions, your answers and the agent's answers. Export it?", "Export comparison", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        var path = Workspace.SaveFile("Platform report JSON|*.json", "hekasomi-comparison.json");
        if (path is not null) await File.WriteAllTextAsync(path, JsonDefaults.Write(new PlatformReport { Evaluations = [SelectedResult.Observation] }));
    });
    [RelayCommand] private Task DeleteImports() => Guard(async _ =>
    {
        if (MessageBox.Show("Remove all imported platform reports from this computer? Files you exported and data on the platform remain there.", "Remove local imports", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await repository.DeletePlatformReportsAsync(); await RefreshInternal(); GlobalAverage = "No global average loaded.";
    });
}
