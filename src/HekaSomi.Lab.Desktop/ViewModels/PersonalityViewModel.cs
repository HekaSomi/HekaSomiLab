using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using HekaSomi.Lab.Desktop.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace HekaSomi.Lab.Desktop.ViewModels;

public sealed record RatingChoice(int? Value, string Label);
public partial class AnswerRow(QuestionnaireItem item) : ObservableObject
{
    public int Id => item.Id; public string Domain => item.Domain; public string Text => item.Text;
    [ObservableProperty] private int? answer;
}
public partial class PersonalityViewModel(LabRepository repository, SettingsViewModel settings, AudioService audio) : PageViewModel
{
    public event Action<PersonalityConstruct>? AssessmentSuggested;
    public ObservableCollection<PersonalityConstruct> Constructs { get; } = [];
    public ObservableCollection<EvidenceEntry> Evidence { get; } = [];
    public ObservableCollection<BehaviorClaim> Claims { get; } = [];
    public ObservableCollection<AnswerRow> Answers { get; } = new(IpipQuestionnaire.Items.OrderBy(i => (i.Id * 17) % 51).Select(i => new AnswerRow(i)));
    public ObservableCollection<DomainScore> Scores { get; } = [];
    public IReadOnlyList<InterviewModule> Modules => InterviewProtocol.Modules;
    public ObservableCollection<Microphone> Microphones { get; } = [];
    public IReadOnlyList<LanguageChoice> SpokenLanguages { get; } = [new("auto", "Detect automatically"), new("en", "English"), new("es", "Spanish"), new("ko", "Korean"), new("fr", "French"), new("ja", "Japanese")];
    [ObservableProperty] private LanguageChoice spokenLanguage = new("auto", "Detect automatically");
    public IReadOnlyList<RatingChoice> RatingChoices { get; } = [new(null, "Skip"), new(1, "1 · Very inaccurate"), new(2, "2 · Moderately inaccurate"), new(3, "3 · Neither"), new(4, "4 · Moderately accurate"), new(5, "5 · Very accurate")];
    private string savedEditorSignature = "";
    private bool revertingSelection;
    private string EditorSignature() => JsonDefaults.Write(new { Name, Style, Evidence, Claims, Answers = Answers.Select(a => new { a.Id, a.Answer }).ToArray() });
    public bool HasUnsavedChanges => (IsDraft && EditorSignature() != savedEditorSignature) || !string.IsNullOrWhiteSpace(Response);
    private void MarkSaved() => savedEditorSignature = EditorSignature();
    private bool ConfirmDiscard() => savedEditorSignature.Length == 0 || !HasUnsavedChanges || System.Windows.MessageBox.Show("Discard unsaved draft edits and any answer text not yet added? Choose No to return and save them.", "Unsaved changes", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;
    [RelayCommand]
    private void RefreshMicrophones()
    {
        if (IsRecording)
            return;
        var number = SelectedMicrophone?.Number;
        Microphones.Clear();
        foreach (var microphone in audio.Microphones)
            Microphones.Add(microphone);
        SelectedMicrophone = Microphones.FirstOrDefault(m => m.Number == number) ?? Microphones.FirstOrDefault();
    }
    [ObservableProperty] private Microphone? selectedMicrophone;
    [ObservableProperty] private PersonalityConstruct? selectedConstruct;
    [ObservableProperty] private string name = "New construct";
    [ObservableProperty] private string style = "Clear, natural, and faithful to the speaker's meaning.";
    [ObservableProperty] private string ownerId = "";
    [ObservableProperty] private string versionLabel = "Draft v1";
    [ObservableProperty] private bool isDraft = true;
    [ObservableProperty] private bool consent;
    [ObservableProperty] private bool ownerReviewed;
    [ObservableProperty] private bool isRecording;
    [ObservableProperty] private InterviewModule selectedModule = InterviewProtocol.Modules[0];
    [ObservableProperty] private string response = "";
    [ObservableProperty] private EvidenceEntry? selectedEvidence;
    [ObservableProperty] private BehaviorClaim? selectedClaim;
    [ObservableProperty] private string preview = "Add evidence, review claims, and approve a version to compile a translation style.";
    private PersonalityConstruct current = new();
    private string? pendingAudio;
    public PersonalityConstruct Current => current;
    public bool HasPendingAudio => pendingAudio is not null;
    public bool CanSwitchConstruct => IsIdle && !IsRecording && !HasPendingAudio;
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(IsBusy) or nameof(IsRecording) or nameof(HasPendingAudio))
            OnPropertyChanged(nameof(CanSwitchConstruct));
    }
    partial void OnSelectedConstructChanged(PersonalityConstruct? value)
    {
        if (value is null || revertingSelection)
            return;
        if (!ConfirmDiscard())
        {
            revertingSelection = true;
            SelectedConstruct = Constructs.FirstOrDefault(p => p.Key == current.Key);
            revertingSelection = false;
            return;
        }
        Load(value);
    }
    private void Load(PersonalityConstruct p)
    {
        // Consent must be confirmed again when switching owners or versions.
        Consent = false;
        // Every editor receives its own deep copy. Approved versions are displayed read-only.
        current = JsonDefaults.Read<PersonalityConstruct>(JsonDefaults.Write(p));
        Name = p.Name;
        Style = p.PreferredStyle;
        OwnerId = p.OwnerId;
        IsDraft = p.Status == ConstructStatus.Draft;
        VersionLabel = $"{p.Status} · version {p.Version}";
        Evidence.Clear();
        foreach (var e in p.Evidence)
            Evidence.Add(e);
        Claims.Clear();
        foreach (var c in current.Claims)
            Claims.Add(c);
        foreach (var a in Answers)
            a.Answer = p.Questionnaire.TryGetValue(a.Id, out var v) ? v : null;
        OwnerReviewed = false;
        Response = "";
        MarkSaved();
        Score();
        Preview = p.Status == ConstructStatus.Approved ? ConstructCompiler.ForTranslation(p) : "Draft: review evidence and approve this version before use.";
    }
    private PersonalityConstruct Snapshot()
    {
        var responses = Answers.Where(a => a.Answer.HasValue).ToDictionary(a => a.Id, a => a.Answer!.Value);
        IpipQuestionnaire.Score(responses);
        return current with
        {
            Name = Name,
            PreferredStyle = Style,
            Evidence = [.. Evidence],
            Claims = [.. Claims],
            Questionnaire = responses,
            ConsentRecordedAt = Consent ? current.ConsentRecordedAt ?? DateTimeOffset.UtcNow : current.ConsentRecordedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
    private void RequireDraft()
    {
        if (IsRecording)
            throw new InvalidOperationException("Stop recording before editing or approving this construct.");
        if (!IsDraft)
            throw new InvalidOperationException("Create a new draft revision to make changes.");
        if (!Consent)
            throw new InvalidOperationException("Confirm the owner's consent before capturing or editing personal data.");
    }
    [RelayCommand] private Task Refresh() => Guard(async _ => await RefreshInternal());
    private async Task RefreshInternal()
    {
        Constructs.Clear();
        foreach (var p in await repository.ListAsync<PersonalityConstruct>("construct"))
            Constructs.Add(p);
    }
    [RelayCommand]
    private void New()
    {
        if (!CanSwitchConstruct)
        {
            Message = "Save or discard the pending recording first.";
            return;
        }
        if (!ConfirmDiscard())
            return;
        SelectedConstruct = null;
        Load(new());
        Message = "New local draft. Confirm consent before capturing personal information.";
    }
    [RelayCommand] private Task Save() => Guard(async _ => { RequireDraft(); current = Snapshot(); await repository.SaveDraftAsync(current); MarkSaved(); await RefreshInternal(); Message = "Draft saved locally."; });
    [RelayCommand]
    private Task AddEvidence() => Guard(async _ =>
    {
        RequireDraft();
        if (string.IsNullOrWhiteSpace(Response))
            throw new InvalidOperationException("Enter or transcribe an answer first.");
        Evidence.Add(new(Guid.NewGuid().ToString("N"), SelectedModule.Id, SelectedModule.Prompt, Response, DateTimeOffset.UtcNow, pendingAudio));
        pendingAudio = null;
        OnPropertyChanged(nameof(HasPendingAudio));
        Response = "";
        current = Snapshot();
        await repository.SaveDraftAsync(current);
        MarkSaved();
        Message = "Interview answer saved. Add a source-linked claim or continue the interview.";
    });
    [RelayCommand]
    private void AddClaim()
    {
        try
        {
            RequireDraft();
            if (SelectedEvidence is null)
                throw new InvalidOperationException("Select an interview answer as the source.");
            var c = new BehaviorClaim { SourceId = SelectedEvidence.Id, Statement = "", Context = "" };
            Claims.Add(c);
            SelectedClaim = c;
        }
        catch (Exception e) { Message = e.Message; }
    }
    [RelayCommand]
    private void RemoveClaim()
    {
        if (IsDraft && SelectedClaim is not null)
            Claims.Remove(SelectedClaim);
    }
    [RelayCommand] private Task SuggestClaims() => Guard(async ct => { RequireDraft(); if (Evidence.Count == 0) throw new InvalidOperationException("Capture interview evidence first."); var proposed = await settings.Client().SuggestClaimsAsync(Snapshot(), settings.CandidateModel, ct); foreach (var c in proposed) Claims.Add(c); Message = $"{proposed.Count} provisional claims. Edit and approve each only if the owner agrees."; });
    [RelayCommand]
    private void Score()
    {
        try
        {
            Scores.Clear();
            foreach (var s in IpipQuestionnaire.Score(Answers.Where(a => a.Answer.HasValue).ToDictionary(a => a.Id, a => a.Answer!.Value)))
                Scores.Add(s);
            Message = "Raw IPIP scores only. Incomplete domains are unscored; these are not clinical or normed results.";
        }
        catch (Exception e) { Message = e.Message; }
    }
    [RelayCommand]
    private Task Approve() => Guard(async _ =>
    {
        RequireDraft();
        if (HasPendingAudio)
            throw new InvalidOperationException("Save or discard the pending recording before approval.");
        if (!string.IsNullOrWhiteSpace(Response))
            throw new InvalidOperationException("Save the interview answer, or clear its text, before approving this version.");
        if (!OwnerReviewed)
            throw new InvalidOperationException("The owner must review the evidence, selected claims and answers before approval.");
        current = Snapshot();
        await repository.ApproveAsync(current);
        Load(current with
        {
            Status = ConstructStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow
        });
        await RefreshInternal();
        Message = "Approved immutable version. Only its communication style can affect translation.";
        AssessmentSuggested?.Invoke(current);
    });
    [RelayCommand] private Task Revise() => Guard(async _ => { if (IsRecording || HasPendingAudio) throw new InvalidOperationException("Save or discard the recording first."); if (!string.IsNullOrWhiteSpace(Response)) throw new InvalidOperationException("Save the interview answer, or clear its text, before creating a new revision."); var versions = await repository.ListAsync<PersonalityConstruct>("construct"); Load(ConstructCompiler.NewRevision(Snapshot(), versions.Where(p => p.Id == current.Id).Select(p => p.Version).DefaultIfEmpty(current.Version).Max() + 1)); Message = "New draft revision. Save it when ready."; });
    [RelayCommand] private Task Revoke() => Guard(async _ => { if (current.Status != ConstructStatus.Approved) throw new InvalidOperationException("Select an approved version."); await repository.RevokeAsync(current); Load(current with { Status = ConstructStatus.Revoked }); await RefreshInternal(); Message = "Version revoked. It cannot be selected for future runs."; });
    [RelayCommand]
    private Task Delete() => Guard(async _ =>
    {
        if (IsRecording)
            throw new InvalidOperationException("Stop recording first.");
        if (System.Windows.MessageBox.Show("Delete every revision of this construct and its locally linked runs, predictions and validation cases? Exported copies are outside this app.", "Delete construct", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
            return;
        var paths = await repository.DeleteConstructAsync(current.Id);
        foreach (var p in paths)
            if (Path.GetFullPath(p).StartsWith(Path.GetFullPath(Workspace.Media) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                File.Delete(p);
        if (pendingAudio is not null)
        {
            File.Delete(pendingAudio);
            pendingAudio = null;
            OnPropertyChanged(nameof(HasPendingAudio));
        }
        Load(new());
        await RefreshInternal();
        Message = "Construct and linked local research records deleted.";
    });
    [RelayCommand]
    private void StartRecording()
    {
        try
        {
            RequireDraft();
            if (IsBusy || IsRecording)
                return;
            if (SelectedMicrophone is null)
                throw new InvalidOperationException("Choose a microphone.");
            if (pendingAudio is not null)
                throw new InvalidOperationException("Save the previous recording as evidence first.");
            audio.StartRecording(SelectedMicrophone.Number);
            IsRecording = true;
            Message = "RECORDING · press Stop recording when finished.";
        }
        catch (Exception e) { Message = e.Message; }
    }
    [RelayCommand] private Task StopRecording() => Guard(async _ => { if (!IsRecording) return; try { var p = await audio.StopRecordingAsync(); pendingAudio = WindowsContentProtector.EncryptRecording(p); OnPropertyChanged(nameof(HasPendingAudio)); Message = "Recording encrypted. Transcribe, review the text, then add it to the interview."; } finally { IsRecording = false; } });
    [RelayCommand] private Task Transcribe() => Guard(async ct => { RequireDraft(); if (pendingAudio is null) throw new InvalidOperationException("Record an answer first."); var plain = WindowsContentProtector.DecryptRecording(pendingAudio); try { Response = await WhisperProcess.TranscribeAsync(plain, await settings.SpeechConfigurationAsync(ct), SpokenLanguage.Code, ct); Message = "Review the transcript before adding it as evidence."; } finally { File.Delete(plain); } });
    [RelayCommand]
    private void DiscardRecording()
    {
        if (IsBusy || IsRecording)
            return;
        try
        {
            if (pendingAudio is not null)
                File.Delete(pendingAudio);
            pendingAudio = null;
            OnPropertyChanged(nameof(HasPendingAudio));
            Message = "Pending recording discarded. Any text in the answer box is still editable.";
        }
        catch (Exception e) { Message = e.Message; }
    }
    [RelayCommand]
    private void PlayEvidence()
    {
        try
        {
            if (SelectedEvidence?.AudioPath is { } p)
                audio.Play(p);
            else
                Message = "Selected evidence has no recording.";
        }
        catch (Exception e) { Message = e.Message; }
    }
    [RelayCommand] private Task ExportStyle() => Guard(async _ => { var saved = (await repository.ListAsync<PersonalityConstruct>("construct")).FirstOrDefault(p => p.Key == current.Key && p.Status == ConstructStatus.Approved) ?? throw new InvalidOperationException("Select a currently approved version."); var p = Workspace.SaveFile("Style JSON|*.json", "translation-style.json"); if (p is not null) { await File.WriteAllTextAsync(p, ConstructCompiler.ForTranslation(saved)); Message = "Exported communication style. The interview and questionnaire are excluded."; } });
}
