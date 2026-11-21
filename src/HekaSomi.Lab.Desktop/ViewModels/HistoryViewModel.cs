using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using HekaSomi.Lab.Desktop.Services;
using System.Collections.ObjectModel;
using System.IO;
namespace HekaSomi.Lab.Desktop.ViewModels;

public partial class HistoryViewModel(LabRepository repository) : PageViewModel
{
    public ObservableCollection<ComparisonRun> Runs { get; } = [];
    public ObservableCollection<CaseResult> Results { get; } = [];
    public ObservableCollection<ReviewRating> Reviews { get; } = [];
    [ObservableProperty] private ComparisonRun? selectedRun;
    [ObservableProperty] private CaseResult? selectedResult;
    [ObservableProperty] private int adequacy = 3;
    [ObservableProperty] private int? style;
    [ObservableProperty] private bool criticalMeaningError;
    [ObservableProperty] private string notes = "";
    partial void OnSelectedRunChanged(ComparisonRun? value)
    {
        SelectedResult = null;
        Results.Clear();
        if (value is not null)
            foreach (var r in value.Results)
                Results.Add(r);
    }
    [ObservableProperty] private string lastExportDirectory = "";
    [RelayCommand]
    private void OpenExport()
    {
        if (Directory.Exists(LastExportDirectory))
            Workspace.OpenLocation(LastExportDirectory);
        else
            Message = "Export a report first.";
    }
    partial void OnSelectedResultChanged(CaseResult? value)
    {
        var review = Reviews.FirstOrDefault(r => r.RunId == SelectedRun?.Id && r.CaseId == value?.CaseId && r.Arm == value?.Arm);
        Adequacy = review?.Adequacy ?? 3;
        Style = review?.Style;
        CriticalMeaningError = review?.CriticalMeaningError ?? false;
        Notes = review?.Notes ?? "";
    }
    [RelayCommand] private Task Refresh() => Guard(async _ => { Runs.Clear(); foreach (var r in await repository.ListAsync<ComparisonRun>("run")) Runs.Add(r); Reviews.Clear(); foreach (var r in await repository.ListAsync<ReviewRating>("review")) Reviews.Add(r); Message = $"{Runs.Count} saved runs · {Reviews.Count} human reviews"; });
    [RelayCommand] private Task Review() => Guard(async _ => { if (SelectedRun is null || SelectedResult is null) throw new InvalidOperationException("Select a run and an output."); if (Adequacy is < 1 or > 5 || Style is < 1 or > 5) throw new InvalidOperationException("Ratings use 1–5; leave style blank if not assessed."); var r = new ReviewRating(SelectedRun.Id, SelectedResult.CaseId, SelectedResult.Arm, Adequacy, Style, CriticalMeaningError, Notes); await repository.SaveReviewAsync(r); Reviews.Clear(); foreach (var item in await repository.ListAsync<ReviewRating>("review")) Reviews.Add(item); Message = "Human review saved separately. One review does not establish translation validity."; });
    [RelayCommand] private Task Export() => Guard(async _ => { if (SelectedRun is null) throw new InvalidOperationException("Select a run."); var p = Workspace.SaveFile("HTML report|*.html", "report.html"); if (p is null) return; var d = Path.Combine(Path.GetDirectoryName(p)!, Path.GetFileNameWithoutExtension(p) + "-" + SelectedRun.Id[..8]); await RunReports.ExportAsync(d, SelectedRun); var ratings = (await repository.ListAsync<ReviewRating>("review")).Where(r => r.RunId == SelectedRun.Id); await File.WriteAllTextAsync(Path.Combine(d, "human-reviews.json"), JsonDefaults.Write(ratings)); LastExportDirectory = d; Message = "Report and separate human reviews exported to " + d; });
}
