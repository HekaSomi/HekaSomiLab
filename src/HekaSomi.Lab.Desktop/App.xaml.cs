using System.Windows;
using System.IO;
using System.Net.Http;
using HekaSomi.Lab.Infrastructure;
using HekaSomi.Lab.Desktop.Services;
using HekaSomi.Lab.Desktop.ViewModels;
namespace HekaSomi.Lab.Desktop;

public partial class App : Application
{
    private Mutex? mutex; private AudioService? audio; private HttpClient? http;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, "Local\\HekaSomi.TranslationLab", out var first);
        if (!first)
        {
            MessageBox.Show("Translation Lab is already open.");
            Shutdown();
            return;
        }
        try
        {
            Directory.CreateDirectory(Workspace.Temporary);
            foreach (var p in Directory.GetFiles(Workspace.Temporary, "*.wav"))
                File.Delete(p);
            http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(4) };
            audio = new AudioService();
            var protector = new WindowsContentProtector();
            var repo = new LabRepository(Path.Combine(Workspace.Root, "lab.db"), protector);
            var settings = new SettingsViewModel(http);
            settings.Load();
            var vm = new MainViewModel(new(settings, audio), new(repo, settings, audio, http), new(repo, settings, audio), new(repo, settings), new(repo), settings, new(repo, http));
            vm.Personality.AssessmentSuggested += p => Dispatcher.BeginInvoke(new Action(async () =>
            {
                if (MessageBox.Show("Your personality profile is approved. Take an optional test and compare your choices with an agent's predictions?", "Try your personality model", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    await vm.Participation.PrepareForAsync(p);
                    vm.SelectedTab = 6;
                }
            }));
            vm.Personality.NewCommand.Execute(null);
            vm.Personality.RefreshMicrophonesCommand.Execute(null);
            var window = new MainWindow { DataContext = vm };
            MainWindow = window;
            window.Show();
            await vm.Benchmark.LoadDemoCommand.ExecuteAsync(null);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Could not open Translation Lab", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        audio?.Dispose();
        http?.Dispose();
        mutex?.Dispose();
        base.OnExit(e);
    }
}
