using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using HekaSomi.Lab.Desktop.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace HekaSomi.Lab.Desktop.ViewModels;

public sealed record LanguageChoice(string Code, string Name);
public partial class QuickTestViewModel : PageViewModel
{
    private readonly SettingsViewModel settings;
    private readonly AudioService audio;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Stopwatch recordingClock = new();
    private string? preparedAudio;
    private bool ownsPrepared;
    public SettingsViewModel Setup => settings;
    public ObservableCollection<Microphone> Microphones { get; } = [];
    public IReadOnlyList<LanguageChoice> Languages { get; } = [new("auto", "Detect automatically"), new("en", "English"), new("es", "Spanish"), new("ko", "Korean"), new("fr", "French"), new("de", "German"), new("ja", "Japanese"), new("zh", "Chinese"), new("pt", "Portuguese"), new("ar", "Arabic"), new("hi", "Hindi")];
    [ObservableProperty] private Microphone? selectedMicrophone;
    [ObservableProperty] private LanguageChoice? selectedLanguage;
    [ObservableProperty] private bool isRecording;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(ProcessLabel))] private bool translateToEnglish;
    [ObservableProperty] private string audioLabel = "Try the sample, record your voice, or choose an audio file.";
    [ObservableProperty] private string result = "";
    [ObservableProperty] private string elapsed = "00:00";
    [ObservableProperty] private string timing = "Your audio is processed on this computer. Nothing is sent to a server.";
    [ObservableProperty] private double recordingLevel;
    [ObservableProperty] private double[] peaks = [];
    public bool CanStart => IsIdle && !IsRecording;
    public bool CanProcess => CanStart && preparedAudio is not null;
    public string ProcessLabel => TranslateToEnglish ? "Translate to English" : "Transcribe audio";
    public bool HasResult => !string.IsNullOrWhiteSpace(Result);

    public QuickTestViewModel(SettingsViewModel settings, AudioService audio)
    {
        this.settings = settings;
        this.audio = audio;
        SelectedLanguage = Languages[0];
        RefreshMicrophones();
        Message = "Ready. Click Try sample to test speech without setting up anything else.";
        timer.Tick += async (_, _) =>
        {
            Elapsed = recordingClock.Elapsed.ToString(@"mm\:ss");
            RecordingLevel = audio.RecordingLevel * 100;
            if (recordingClock.Elapsed >= TimeSpan.FromMinutes(2) && IsRecording && !IsBusy)
                await StopRecordingCommand.ExecuteAsync(null);
        };
    }
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy) or nameof(IsRecording))
        {
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanProcess));
        }
        if (e.PropertyName == nameof(Result))
            OnPropertyChanged(nameof(HasResult));
    }
    [RelayCommand]
    private void RefreshMicrophones()
    {
        if (IsRecording)
            return;
        var selected = SelectedMicrophone?.Number;
        Microphones.Clear();
        foreach (var microphone in audio.Microphones)
            Microphones.Add(microphone);
        SelectedMicrophone = Microphones.FirstOrDefault(m => m.Number == selected) ?? Microphones.FirstOrDefault();
    }
    private void SetAudio(string path, bool owned, string label)
    {
        audio.StopPlayback();
        if (ownsPrepared && preparedAudio is not null)
            File.Delete(preparedAudio);
        preparedAudio = path;
        ownsPrepared = owned;
        AudioLabel = label;
        Result = "";
        Peaks = WaveFile.Inspect(path).Peaks;
        OnPropertyChanged(nameof(CanProcess));
    }
    [RelayCommand]
    private Task TrySample() => Guard(async ct =>
    {
        if (IsRecording)
            return;
        SetAudio(Path.Combine(AppContext.BaseDirectory, "samples", "demo-1.wav"), false, "Included sample · Please do not cancel my reservation.");
        await Process(ct);
    });
    [RelayCommand]
    private Task ChooseAudio() => Guard(async ct =>
    {
        if (IsRecording)
            return;
        var path = Workspace.OpenFile(AudioPreparation.Filter);
        if (path is null)
            return;
        Message = "Preparing your audio…";
        SetAudio(await AudioPreparation.PrepareAsync(path, Workspace.Temporary, ct), true, Path.GetFileName(path));
        Message = "Audio ready. Choose the language or leave automatic detection, then transcribe.";
    });
    [RelayCommand]
    private void StartRecording()
    {
        if (!CanStart)
            return;
        try
        {
            if (SelectedMicrophone is null)
                throw new InvalidOperationException("No microphone was found. Connect one and refresh microphones, or choose an audio file.");
            audio.StopPlayback();
            audio.StartRecording(SelectedMicrophone.Number);
            IsRecording = true;
            recordingClock.Restart();
            Elapsed = "00:00";
            timer.Start();
            Message = "Recording… speak clearly, then click Stop. Recording stops automatically after 2 minutes.";
        }
        catch (Exception e) { Message = UserFeedback.Explain(e); ErrorDetails = e.Message; }
    }
    [RelayCommand]
    private Task StopRecording() => Guard(async _ =>
    {
        if (!IsRecording)
            return;
        try
        {
            SetAudio(await audio.StopRecordingAsync(), true, "Microphone recording · " + recordingClock.Elapsed.ToString(@"mm\:ss"));
            Message = "Recording ready. Click Transcribe audio. Use Personality studio to keep interview evidence.";
        }
        finally { timer.Stop(); recordingClock.Stop(); IsRecording = false; RecordingLevel = 0; }
    });
    [RelayCommand] private Task Transcribe() => Guard(Process);
    private async Task Process(CancellationToken ct)
    {
        if (IsRecording)
            throw new InvalidOperationException("Stop recording before transcribing.");
        if (preparedAudio is null)
            throw new InvalidOperationException("Record, import audio, or click Try sample first.");
        Result = "";
        Message = "Checking the speech engine…";
        var config = await settings.SpeechConfigurationAsync(ct);
        var watch = Stopwatch.StartNew();
        Message = TranslateToEnglish ? "Translating speech into English on this computer…" : "Transcribing speech on this computer…";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            Result = await WhisperProcess.TranscribeAsync(preparedAudio, config, SelectedLanguage?.Code ?? "auto", timeout.Token, TranslateToEnglish);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("This clip took more than 5 minutes. Try a shorter clip or close other intensive applications.");
        }
        Timing = $"Finished in {watch.Elapsed.TotalSeconds:0.0} s · actual local inference · {(TranslateToEnglish ? "English translation" : "original-language transcript")}";
        Message = "Done. Review the text; you can copy it or save a text file.";
    }
    [RelayCommand]
    private void CopyResult()
    {
        try
        {
            if (HasResult)
            {
                Clipboard.SetText(Result);
                Message = "Text copied.";
            }
        }
        catch (Exception e) { Message = e.Message; }
    }
    [RelayCommand]
    private Task SaveResult() => Guard(async ct =>
    {
        if (!HasResult)
            throw new InvalidOperationException("Transcribe audio first.");
        var path = Workspace.SaveFile("Text file|*.txt", "transcript.txt");
        if (path is not null)
        {
            await File.WriteAllTextAsync(path, Result, ct);
            Message = "Text saved to " + path;
        }
    });
    [RelayCommand]
    private void Play()
    {
        try
        {
            if (preparedAudio is not null)
                audio.Play(preparedAudio);
        }
        catch (Exception e) { Message = e.Message; }
    }
    [RelayCommand] private void StopPlayback() => audio.StopPlayback();
    [RelayCommand]
    private void Clear()
    {
        if (!CanStart)
            return;
        Cleanup();
        AudioLabel = "Choose a sample, microphone or audio file.";
        Result = "";
        Peaks = [];
        Timing = "Your audio is processed on this computer. Nothing is sent to a server.";
        Message = "Quick-test audio and text cleared.";
        OnPropertyChanged(nameof(CanProcess));
    }
    public void Cleanup()
    {
        timer.Stop();
        audio.StopPlayback();
        if (ownsPrepared && preparedAudio is not null)
        {
            try
            {
                File.Delete(preparedAudio);
            }
            catch (IOException) { }
        }
        preparedAudio = null;
        ownsPrepared = false;
    }
}
