using System.Windows;
using System.ComponentModel;
using HekaSomi.Lab.Desktop.ViewModels;
namespace HekaSomi.Lab.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    private int currentPage;
    private bool restoring;
    private readonly HashSet<int> loadedPages = [];
    private async void OnPageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (restoring || !ReferenceEquals(e.OriginalSource, Pages))
            return;
        if (DataContext is MainViewModel { HasWork: true })
        {
            restoring = true;
            Pages.SelectedIndex = currentPage;
            restoring = false;
            MessageBox.Show("Finish or cancel the active operation before switching workspaces.", "Work in progress");
        }
        else
        {
            currentPage = Pages.SelectedIndex;
            if (DataContext is MainViewModel vm && loadedPages.Add(currentPage))
            {
                if (currentPage == 1)
                    await vm.Benchmark.RefreshConstructsCommand.ExecuteAsync(null);
                else if (currentPage == 2)
                    await vm.Personality.RefreshCommand.ExecuteAsync(null);
                else if (currentPage == 3)
                    await vm.Validation.RefreshCommand.ExecuteAsync(null);
                else if (currentPage == 4)
                    await vm.History.RefreshCommand.ExecuteAsync(null);
                else if (currentPage == 6)
                    await vm.Participation.RefreshCommand.ExecuteAsync(null);
            }
        }
    }
    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.QuickTest.Cleanup();
        base.OnClosed(e);
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel { HasWork: true })
        {
            e.Cancel = true;
            MessageBox.Show("Stop recording or cancel the active operation before closing.", "Work in progress");
        }
        if (!e.Cancel && DataContext is MainViewModel vm && vm.Personality.HasUnsavedChanges)
        {
            var result = MessageBox.Show("Discard unsaved personality edits and interview answer text, and close? Choose No to return and save them.", "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                e.Cancel = true;
        }
        base.OnClosing(e);
    }
}
