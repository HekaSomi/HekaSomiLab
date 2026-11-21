using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using HekaSomi.Lab.Desktop.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;

namespace HekaSomi.Lab.Desktop.ViewModels;

public sealed record EngineChoice(EngineKind Kind, string Label);
public partial class BenchmarkViewModel(LabRepository repository, SettingsViewModel settings, AudioService audio, HttpClient http) : PageViewModel
{
    public ObservableCollection<AudioCase> Cases { get; } = [];
    public ObservableCollection<CaseResult> Results { get; } = [];
    public ObservableCollection<GateResult> Gates { get; } = [];
    public ObservableCollection<PersonalityConstruct> Constructs { get; } = [];
    public IReadOnlyList<EngineChoice> EngineChoices { get; } = [new(EngineKind.Replay, "Demo replay · no model needed"), new(EngineKind.TextAndOllama, "Translate text · language model"), new(EngineKind.WhisperAndOllama, "Translate audio · speech + language model")];
    [ObservableProperty] private CaseResult? selectedResult;
    [ObservableProperty] private string lastExportDirectory = "";
    [RelayCommand]
    private void NewDataset()
    {
        if (IsBusy)
            return;
        Cases.Clear();
        Results.Clear();
        Gates.Clear();
        SelectedCase = null;
        SelectedResult = null;
        lastRun = null;
        datasetId = Guid.NewGuid().ToString("N");
        DatasetName = "My comparison";
        Message = "New empty comparison. Add a sample below.";
    }
    [RelayCommand]
    private void CopyTranslation()
    {
        try
        {
            if (SelectedResult?.Output is { } output)
            {
                System.Windows.Clipboard.SetText(output.Translation);
                Message = "Translation copied.";
            }
            else
                Message = "Select a completed output first.";
        }
        catch (Exception e) { Message = e.Message; }
    }
    [RelayCommand]
    private void OpenExport()
    {
        if (Directory.Exists(LastExportDirectory))
            Workspace.OpenLocation(LastExportDirectory);
        else
            Message = "Export a report first.";
    }
    [ObservableProperty] private EngineKind engineKind = EngineKind.Replay;
    [ObservableProperty] private string datasetName = "New comparison";
    [ObservableProperty] private string sourceLanguage = "en";
    [ObservableProperty] private string targetLanguage = "Spanish";
    [ObservableProperty] private string sampleName = "New sample";
    [ObservableProperty] private string audioPath = "";
    [ObservableProperty] private string textInput = "";
    [ObservableProperty] private string referenceTranscript = "";
    [ObservableProperty] private string referenceTranslation = "";
    [ObservableProperty] private AudioCase? selectedCase;
    [ObservableProperty] private PersonalityConstruct? selectedConstruct;
    [ObservableProperty] private bool usePersonality;
    [ObservableProperty] private double progress;
    [ObservableProperty] private double[] peaks = [];
    [ObservableProperty] private double seekPercent;
    [ObservableProperty] private string waveDetails = "Select a WAV sample to inspect its waveform.";
    [ObservableProperty] private string summary = "Run the demo or import a dataset. Replay is a fixture, not a live model.";
    private ComparisonRun? lastRun;
    private string datasetId = Guid.NewGuid().ToString("N");
    partial void OnSelectedCaseChanged(AudioCase? value)
    {
        try
        {
            Peaks = [];
            if (value?.AudioPath is null)
                return;
            var w = WaveFile.Inspect(value.AudioPath);
            Peaks = w.Peaks;
            WaveDetails = $"{w.DurationSeconds:0.0} s · {w.SampleRate:N0} Hz · {w.Channels} channel(s) · clip fraction {w.ClippedFraction:P2}";
        }
        catch (Exception e) { WaveDetails = e.Message; }
    }
    [RelayCommand] private Task LoadDemo() => Guard(async ct => { Load(await DatasetFiles.LoadAsync(Path.Combine(AppContext.BaseDirectory, "samples", "demo.json"))); EngineKind = EngineKind.Replay; });
    [RelayCommand] private Task Import() => Guard(async ct => { var p = Workspace.OpenFile("Dataset manifest|*.json"); if (p is not null) Load(await DatasetFiles.LoadAsync(p)); });
    private void Load(Dataset d)
    {
        datasetId = d.Id;
        Cases.Clear();
        foreach (var c in d.Cases)
            Cases.Add(c);
        DatasetName = d.Name;
        SourceLanguage = d.SourceLanguage;
        TargetLanguage = d.TargetLanguage;
        SelectedCase = Cases.FirstOrDefault();
        Message = $"Loaded {Cases.Count} samples.";
    }
    private Dataset Snapshot() => new() { Id = datasetId, Name = DatasetName, SourceLanguage = SourceLanguage, TargetLanguage = TargetLanguage, Cases = [.. Cases] };
    [RelayCommand] private Task SaveDataset() => Guard(async ct => { var p = Workspace.SaveFile("Dataset JSON|*.json", "dataset.json"); if (p is not null) { await DatasetFiles.SaveAsync(p, Snapshot()); Message = "Dataset exported. It contains references and local audio paths."; } });
    [RelayCommand]
    private void BrowseAudio()
    {
        var p = Workspace.OpenFile(AudioPreparation.Filter);
        if (p is not null)
        {
            AudioPath = p;
            SampleName = Path.GetFileNameWithoutExtension(p);
        }
    }
    [RelayCommand]
    private void AddSample()
    {
        if (IsBusy)
            return;
        if (string.IsNullOrWhiteSpace(TextInput) && string.IsNullOrWhiteSpace(AudioPath))
        {
            Message = "Enter source text or choose an audio file first.";
            return;
        }
        if (!string.IsNullOrWhiteSpace(AudioPath) && !File.Exists(AudioPath))
        {
            Message = "Choose an existing audio file.";
            return;
        }
        var c = new AudioCase { Name = SampleName, AudioPath = string.IsNullOrWhiteSpace(AudioPath) ? null : AudioPath, TextInput = TextInput, ReferenceTranscript = ReferenceTranscript, ReferenceTranslation = ReferenceTranslation };
        Cases.Add(c);
        SelectedCase = c;
        if (EngineKind == EngineKind.Replay)
            EngineKind = c.AudioPath is null ? EngineKind.TextAndOllama : EngineKind.WhisperAndOllama;
        TextInput = AudioPath = ReferenceTranscript = ReferenceTranslation = "";
        SampleName = "New sample";
        Message = "Sample added. Choose language models in Setup to compare live translations.";
    }
    [RelayCommand]
    private void RemoveSample()
    {
        if (!IsBusy && SelectedCase is not null)
            Cases.Remove(SelectedCase);
    }
    [RelayCommand] private Task RefreshConstructs() => Guard(async _ => { Constructs.Clear(); foreach (var p in await repository.ListAsync<PersonalityConstruct>("construct")) if (p.Status == ConstructStatus.Approved) Constructs.Add(p); });
    [RelayCommand]
    private Task Run() => Guard(async ct =>
    {
        if (Cases.Count == 0)
            throw new InvalidOperationException("Add a sample or load the demo first.");
        Progress = 0;
        PersonalityConstruct? construct = null;
        if (UsePersonality)
        {
            construct = (await repository.ListAsync<PersonalityConstruct>("construct")).FirstOrDefault(c => c.Key == SelectedConstruct?.Key && c.Status == ConstructStatus.Approved)
                ?? throw new InvalidOperationException("Choose a currently approved construct, or turn off personality style.");
            if (EngineKind == EngineKind.Replay)
                throw new InvalidOperationException("Replay cannot test a personality change. Select a live engine.");
        }
        var b = settings.Configuration(EngineKind, false);
        var c = settings.Configuration(EngineKind, true, UsePersonality);
        var dataset = Snapshot();
        var temporary = new List<string>();
        try
        {
            if (EngineKind == EngineKind.WhisperAndOllama)
            {
                await settings.SpeechConfigurationAsync(ct);
                var normalized = new List<AudioCase>();
                foreach (var sample in dataset.Cases)
                {
                    if (sample.AudioPath is null)
                        throw new InvalidOperationException($"Choose audio for {sample.Name}.");
                    Message = "Preparing audio: " + sample.Name;
                    var path = await AudioPreparation.PrepareAsync(sample.AudioPath, Workspace.Temporary, ct);
                    temporary.Add(path);
                    normalized.Add(sample with
                    {
                        AudioPath = path
                    });
                }
                dataset = dataset with
                {
                    Cases = normalized
                };
            }
            Results.Clear();
            Gates.Clear();
            SelectedResult = null;
            lastRun = await new BenchmarkRunner(new TranslationEngine(http)).RunAsync(dataset, b, c, construct,
                new Progress<RunProgress>(p => { Progress = 100.0 * p.Completed / p.Total; Message = p.Message; }), ct);
        }
        finally { foreach (var path in temporary) File.Delete(path); }
        await repository.SaveRunAsync(lastRun);
        foreach (var result in lastRun.Results)
            Results.Add(result);
        foreach (var gate in lastRun.Gates)
            Gates.Add(gate);
        var bw = Metrics.AggregateWer(lastRun.Results.Where(r => r.Arm == "Baseline"));
        var cw = Metrics.AggregateWer(lastRun.Results.Where(r => r.Arm == "Candidate"));
        var bp = Metrics.Percentile(lastRun.Results.Where(r => r.Arm == "Baseline").Select(r => r.Output?.TotalMs).OfType<double>(), .95);
        var cp = Metrics.Percentile(lastRun.Results.Where(r => r.Arm == "Candidate").Select(r => r.Output?.TotalMs).OfType<double>(), .95);
        static string Rate(double? v) => v?.ToString("P1") ?? "N/A";
        static string Time(double? v) => v?.ToString("N0") + (v.HasValue ? " ms" : "N/A");
        Summary = $"{lastRun.State} · WER {Rate(bw)} / {Rate(cw)} · offline p95 {Time(bp)} / {Time(cp)} · {lastRun.Results.Count(r => r.Error is not null)} errors";
        Message = "Run saved. WER measures transcription only; human meaning review is still required.";
    });
    [RelayCommand]
    private Task Export() => Guard(async _ =>
    {
        if (lastRun is null)
            throw new InvalidOperationException("Run a comparison first.");
        var p = Workspace.SaveFile("HTML report|*.html", "report.html");
        if (p is null)
            return;
        var directory = Path.Combine(Path.GetDirectoryName(p)!, Path.GetFileNameWithoutExtension(p) + "-" + lastRun.Id[..8]);
        await RunReports.ExportAsync(directory, lastRun);
        LastExportDirectory = directory;
        Message = "Exported report.html, results.csv and run.json to " + directory;
    });
    [RelayCommand]
    private void Play()
    {
        try
        {
            if (SelectedCase?.AudioPath is { } p)
                audio.Play(p);
            else
                Message = "This sample has no audio.";
        }
        catch (Exception e) { Message = e.Message; }
    }
    [RelayCommand] private void StopAudio() => audio.StopPlayback();
    [RelayCommand]
    private void Seek()
    {
        audio.Position = audio.Duration * SeekPercent / 100;
    }
}
