using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using HekaSomi.Lab.Desktop.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;

namespace HekaSomi.Lab.Desktop.ViewModels;

public sealed record ConnectionSettings(string OllamaUrl, string BaselineModel, string CandidateModel, string WhisperExecutable, string WhisperModel, bool AllowRemote, bool UseBundledSpeech = true);
public partial class SettingsViewModel(HttpClient http) : PageViewModel
{
    [ObservableProperty] private string ollamaUrl = "http://localhost:11434";
    [ObservableProperty] private string baselineModel = "";
    [ObservableProperty] private string candidateModel = "";
    [ObservableProperty] private string whisperExecutable = "";
    [ObservableProperty] private string whisperModel = "";
    [ObservableProperty] private bool allowRemote;
    [ObservableProperty] private bool useBundledSpeech = true;
    [ObservableProperty] private string speechStatus = "Checking included speech files…";
    [ObservableProperty] private string translationStatus = "Optional · connect a language model for other translation languages and AI suggestions.";
    public ObservableCollection<string> Models { get; } = [];
    private SpeechBundle? bundle;
    private bool bundleVerified;
    private string SettingsPath => Path.Combine(Workspace.Root, "connections.json");

    public void Load()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                var saved = JsonDefaults.Read<ConnectionSettings>(File.ReadAllText(SettingsPath));
                OllamaUrl = saved.OllamaUrl;
                BaselineModel = saved.BaselineModel;
                CandidateModel = saved.CandidateModel;
                WhisperExecutable = saved.WhisperExecutable;
                WhisperModel = saved.WhisperModel;
                AllowRemote = saved.AllowRemote;
                UseBundledSpeech = saved.UseBundledSpeech;
            }
            catch (Exception e) { Message = "Saved settings could not be loaded. Included speech is still available. " + e.Message; }
        }
        DiscoverBundle();
    }
    private void DiscoverBundle()
    {
        try
        {
            bundle = BundledSpeech.Resolve(AppContext.BaseDirectory);
            SpeechStatus = $"Included · {bundle.Manifest.ModelName} · no download or account needed";
        }
        catch (Exception e) { SpeechStatus = e.Message; }
    }
    partial void OnUseBundledSpeechChanged(bool value) => SpeechStatus = value
        ? (bundle is null ? "Click Check speech package to check the included files." : $"Included · {bundle.Manifest.ModelName}")
        : "Custom engine selected · choose both files in Advanced settings.";

    public async Task<EngineConfiguration> SpeechConfigurationAsync(CancellationToken ct)
    {
        if (UseBundledSpeech)
        {
            bundle ??= BundledSpeech.Resolve(AppContext.BaseDirectory);
            if (!bundleVerified)
            {
                SpeechStatus = "Checking included speech files…";
                await bundle.VerifyAsync(ct);
                bundleVerified = true;
                SpeechStatus = $"Ready · {bundle.Manifest.ModelName} · runs on this computer";
            }
            return new()
            {
                Name = "Included speech",
                WhisperExecutable = bundle.ExecutablePath,
                WhisperModel = bundle.ModelPath,
                TimeoutSeconds = 300
            };
        }
        if (!File.Exists(WhisperExecutable) || !File.Exists(WhisperModel))
            throw new InvalidOperationException("Choose Use included speech engine, or select both custom speech files in Advanced settings.");
        return new()
        {
            Name = "Custom speech",
            WhisperExecutable = WhisperExecutable,
            WhisperModel = WhisperModel,
            TimeoutSeconds = 300
        };
    }
    public void ValidateEndpoint()
    {
        if (!Uri.TryCreate(OllamaUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Enter a valid model server address.");
        if (!uri.IsLoopback && !AllowRemote)
            throw new InvalidOperationException("Enable remote processing in Setup before sending data off this computer.");
        if (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            throw new InvalidOperationException("Remote endpoints must use HTTPS.");
    }
    public OllamaClient Client()
    {
        ValidateEndpoint();
        return new(http, OllamaUrl);
    }
    public EngineConfiguration Configuration(EngineKind kind, bool candidate, bool personality = false)
    {
        if (kind != EngineKind.Replay)
        {
            ValidateEndpoint();
            if (string.IsNullOrWhiteSpace(candidate ? CandidateModel : BaselineModel))
                throw new InvalidOperationException("Choose a language model in Setup for this comparison. Speech testing on Start works without a model server.");
        }
        return new()
        {
            Name = candidate ? "Candidate" : "Baseline",
            Kind = kind,
            OllamaUrl = OllamaUrl,
            Model = candidate ? CandidateModel : BaselineModel,
            UsePersonality = personality,
            WhisperExecutable = UseBundledSpeech ? bundle?.ExecutablePath ?? "" : WhisperExecutable,
            WhisperModel = UseBundledSpeech ? bundle?.ModelPath ?? "" : WhisperModel
        };
    }
    [RelayCommand]
    private Task CheckSpeech() => Guard(async ct =>
    {
        bundleVerified = false;
        await SpeechConfigurationAsync(ct);
        Message = "Speech files are ready. Open Start and click Try sample, or record your voice.";
    });
    [RelayCommand]
    private void RestoreBundled()
    {
        UseBundledSpeech = true;
        bundleVerified = false;
        DiscoverBundle();
        Message = "Using the included speech engine and multilingual model.";
    }
    [RelayCommand]
    private Task Connect() => Guard(async ct =>
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        List<string> models;
        try
        {
            models = await Client().ModelsAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The model server did not respond. Open Ollama and reconnect. Speech testing on Start still works.");
        }
        Models.Clear();
        foreach (var model in models)
            Models.Add(model);
        if (!models.Contains(BaselineModel))
            BaselineModel = models.FirstOrDefault() ?? "";
        if (!models.Contains(CandidateModel))
            CandidateModel = BaselineModel;
        TranslationStatus = models.Count == 0 ? "Connected · no language models installed" : $"Connected · {models.Count} installed models";
        Message = models.Count == 0
            ? "Install a model in Ollama, then connect again. Included speech and English audio translation already work."
            : "Choose models below and save your setup.";
    });
    [RelayCommand] private void OpenOllama() => Workspace.OpenLocation("https://ollama.com/download/windows");
    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(Workspace.Root);
        Workspace.OpenLocation(Workspace.Root);
    }
    [RelayCommand]
    private void BrowseWhisper()
    {
        var path = Workspace.OpenFile("Whisper CLI|*.exe");
        if (path is not null)
        {
            WhisperExecutable = path;
            UseBundledSpeech = false;
        }
    }
    [RelayCommand]
    private void BrowseModel()
    {
        var path = Workspace.OpenFile("GGML model|*.bin|All files|*.*");
        if (path is not null)
        {
            WhisperModel = path;
            UseBundledSpeech = false;
        }
    }
    [RelayCommand]
    private Task Save() => Guard(async _ =>
    {
        ValidateEndpoint();
        Directory.CreateDirectory(Workspace.Root);
        var temporary = SettingsPath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonDefaults.Write(new ConnectionSettings(OllamaUrl, BaselineModel, CandidateModel, WhisperExecutable, WhisperModel, AllowRemote, UseBundledSpeech)));
        File.Move(temporary, SettingsPath, true);
        Message = UseBundledSpeech ? "Setup saved. Included speech is selected." : "Setup saved. Your custom speech engine is selected.";
    });
}
