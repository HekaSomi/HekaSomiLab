using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HekaSomi.Lab.Desktop.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
    [ObservableProperty] private string message = "Ready";
    [ObservableProperty] private string errorDetails = "";
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsIdle))] private bool isBusy;
    public bool IsIdle => !IsBusy;
    protected CancellationTokenSource? Operation;
    protected async Task Guard(Func<CancellationToken, Task> action)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        ErrorDetails = "";
        Operation = new();
        try
        {
            await action(Operation.Token);
        }
        catch (OperationCanceledException) { Message = "Canceled. Completed work has been retained."; }
        catch (Exception e) { Message = Services.UserFeedback.Explain(e); ErrorDetails = e.Message; }
        finally { Operation.Dispose(); Operation = null; IsBusy = false; }
    }
    [RelayCommand] private void Cancel() => Operation?.Cancel();
}
