using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace HekaSomi.Lab.Desktop.ViewModels;

public partial class MainViewModel(QuickTestViewModel quickTest, BenchmarkViewModel benchmark, PersonalityViewModel personality, ValidationViewModel validation, HistoryViewModel history, SettingsViewModel settings, ParticipationViewModel participation) : ObservableObject
{
    public QuickTestViewModel QuickTest { get; } = quickTest;
    public BenchmarkViewModel Benchmark { get; } = benchmark;
    public PersonalityViewModel Personality { get; } = personality;
    public ValidationViewModel Validation { get; } = validation;
    public HistoryViewModel History { get; } = history;
    public SettingsViewModel Settings { get; } = settings;
    public ParticipationViewModel Participation { get; } = participation;
    [ObservableProperty] private int selectedTab;
    public bool HasWork => QuickTest.IsBusy || QuickTest.IsRecording || Benchmark.IsBusy || Personality.IsBusy || Personality.IsRecording || Personality.HasPendingAudio || Validation.IsBusy || History.IsBusy || Settings.IsBusy || Participation.IsBusy;
    [RelayCommand] private void GoToBench() => SelectedTab = 1;
    [RelayCommand] private void GoToPersonality() => SelectedTab = 2;
    [RelayCommand] private void GoToSetup() => SelectedTab = 5;
}
