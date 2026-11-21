using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using HekaSomi.Lab.Desktop.Services;
using System.Collections.ObjectModel;
using System.IO;
namespace HekaSomi.Lab.Desktop.ViewModels;

public partial class ValidationViewModel(LabRepository repository, SettingsViewModel settings) : PageViewModel
{
    public ObservableCollection<PersonalityConstruct> Constructs { get; } = [];
    public ObservableCollection<ValidationStimulus> Cases { get; } = [];
    public ObservableCollection<ValidationSummary> Summaries { get; } = [];
    public ObservableCollection<PersonaPrediction> Predictions { get; } = [];
    [ObservableProperty] private PersonalityConstruct? selectedConstruct;
    [ObservableProperty] private ValidationStimulus? selectedCase;
    [ObservableProperty] private string question = "";
    [ObservableProperty] private string optionsText = "Option A\nOption B\nOption C";
    [ObservableProperty] private string answerT0 = "";
    [ObservableProperty] private string answerT1 = "";
    [ObservableProperty] private bool heldOutConfirmed;
    [ObservableProperty] private string summary = "Collect new held-out choices after approving a construct. T0 is sealed separately from model inputs.";
    [RelayCommand] private Task Refresh() => Guard(async _ => { Constructs.Clear(); foreach (var p in await repository.ListAsync<PersonalityConstruct>("construct")) if (p.Status == ConstructStatus.Approved) Constructs.Add(p); await LoadCases(); });
    [RelayCommand] private Task LoadSelected() => Guard(async _ => await LoadCases());
    private async Task LoadCases()
    {
        Cases.Clear();
        foreach (var c in await repository.ListAsync<ValidationStimulus>("stimulus"))
            if (c.OwnerId == SelectedConstruct?.OwnerId)
                Cases.Add(c);
        await Summarize();
    }
    private async Task<PersonalityConstruct> Approved() => (await repository.ListAsync<PersonalityConstruct>("construct")).FirstOrDefault(p => p.Key == SelectedConstruct?.Key && p.Status == ConstructStatus.Approved) ?? throw new InvalidOperationException("Select a currently approved construct.");
    [RelayCommand]
    private Task Seal() => Guard(async _ =>
    {
        var p = await Approved();
        var options = OptionsText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        if (!HeldOutConfirmed)
            throw new InvalidOperationException("Confirm this is new held-out material, absent from interview and questionnaire inputs.");
        if (string.IsNullOrWhiteSpace(Question) || options.Count < 2 || !options.Contains(AnswerT0.Trim()))
            throw new InvalidOperationException("Enter a question, at least two distinct options, and a T0 answer exactly matching one option.");
        var c = new ValidationStimulus(Guid.NewGuid().ToString("N"), p.OwnerId, Question.Trim(), options);
        await repository.SealCaseAsync(c, new(c.Id, p.OwnerId, AnswerT0.Trim()));
        AnswerT0 = "";
        Question = "";
        HeldOutConfirmed = false;
        await LoadCases();
        Message = "T0 sealed. The prediction service cannot read the answer key.";
    });
    [RelayCommand]
    private Task Predict() => Guard(async ct =>
    {
        var p = await Approved();
        var cases = (await repository.ListAsync<ValidationStimulus>("stimulus")).Where(c => c.OwnerId == p.OwnerId).ToArray();
        if (cases.Length == 0)
            throw new InvalidOperationException("Seal at least one held-out case first.");
        var client = settings.Client();
        var existing = await repository.ListAsync<PersonaPrediction>("prediction");
        if (existing.Any(e => e.ConstructId == p.Id && e.ConstructVersion == p.Version && e.Model != settings.CandidateModel))
            throw new InvalidOperationException("This version already has predictions from a different model. Restore that model name or use a new construct revision.");
        var count = 0;
        try
        {
            foreach (var c in cases)
                foreach (var condition in Enum.GetValues<PersonaCondition>())
                {
                    if (existing.Any(e => e.CaseId == c.Id && e.ConstructId == p.Id && e.ConstructVersion == p.Version && e.Condition == condition))
                        continue;
                    ct.ThrowIfCancellationRequested();
                    Message = $"Predicting {condition} · {++count}/{cases.Length * 5}";
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromMinutes(3));
                    var choice = await client.PredictAsync(c, ConstructCompiler.ForPrediction(p, condition), settings.CandidateModel, timeout.Token);
                    await repository.SavePredictionAsync(new(c.Id, p.OwnerId, p.Id, p.Version, Metrics.Hash(JsonDefaults.Write(p)), condition, choice, settings.CandidateModel, DateTimeOffset.UtcNow));
                }
        }
        finally { await Summarize(); }
        Message = "Five conditions evaluated. Saved predictions are frozen; repeated clicks do not select new answers.";
    });
    [RelayCommand]
    private Task Retest() => Guard(async _ =>
    {
        if (SelectedCase is null || !SelectedCase.Options.Contains(AnswerT1.Trim()))
            throw new InvalidOperationException("Select a case and enter a T1 answer exactly matching one of its options.");
        var a = (await repository.ListAsync<SealedAnswers>("answer")).Single(a => a.CaseId == SelectedCase.Id);
        await repository.SaveAnswersAsync(a with
        {
            T1 = AnswerT1.Trim()
        });
        AnswerT1 = "";
        await Summarize();
        Message = "Retest saved. Use the planned study interval; this app does not certify retest reliability.";
    });
    private async Task Summarize()
    {
        Predictions.Clear();
        Summaries.Clear();
        if (SelectedConstruct is null)
            return;
        var all = await repository.ListAsync<PersonaPrediction>("prediction");
        var selected = all.Where(p => p.ConstructId == SelectedConstruct.Id && p.ConstructVersion == SelectedConstruct.Version).ToArray();
        foreach (var p in selected)
            Predictions.Add(p);
        var answers = (await repository.ListAsync<SealedAnswers>("answer")).ToDictionary(a => a.CaseId);
        foreach (var s in BehavioralEvaluation.Summarize(selected, answers))
            Summaries.Add(s);
        Summary = $"{selected.Select(p => p.CaseId).Distinct().Count()} held-out cases · one owner · descriptive results only. Retest agreement is shown separately; no population or validity claim.";
    }
    [RelayCommand] private Task Export() => Guard(async _ => { await Summarize(); var p = Workspace.SaveFile("Validation JSON|*.json", "validation.json"); if (p is not null) { await File.WriteAllTextAsync(p, JsonDefaults.Write(new { constructKey = SelectedConstruct?.Key, summary = Summary, metrics = Summaries, predictions = Predictions })); Message = "Exported predictions and descriptive metrics; raw answer keys are excluded."; } });
}
