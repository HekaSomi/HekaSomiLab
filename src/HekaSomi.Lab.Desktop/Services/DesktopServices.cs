using System.IO;
using System.Security.Cryptography;
using System.Text;
using HekaSomi.Lab.Infrastructure;
using NAudio.Wave;
using Microsoft.Win32;

namespace HekaSomi.Lab.Desktop.Services;

public static class Workspace
{
    public static void OpenLocation(string path) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HekaSomi", "TranslationLab");
    public static string Media => Path.Combine(Root, "media");
    public static string Temporary => Path.Combine(Root, "temporary");
    public static string? OpenFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
    public static string? SaveFile(string filter, string name)
    {
        var dialog = new SaveFileDialog { FileName = name, Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
public sealed class WindowsContentProtector : IContentProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HekaSomi.TranslationLab.v1");
    public string Protect(string plaintext) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser));
    public string Unprotect(string ciphertext) => Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ciphertext), Entropy, DataProtectionScope.CurrentUser));
    public static string EncryptRecording(string path)
    {
        Directory.CreateDirectory(Workspace.Media);
        var destination = Path.Combine(Workspace.Media, Guid.NewGuid().ToString("N") + ".hkaudio");
        File.WriteAllBytes(destination, ProtectedData.Protect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser));
        File.Delete(path);
        return destination;
    }
    public static string DecryptRecording(string path)
    {
        Directory.CreateDirectory(Workspace.Temporary);
        var output = Path.Combine(Workspace.Temporary, Guid.NewGuid().ToString("N") + ".wav");
        File.WriteAllBytes(output, ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser));
        return output;
    }
}
public record Microphone(int Number, string Name);
public sealed class AudioService : IDisposable
{
    private WaveInEvent? input; private WaveFileWriter? writer; private TaskCompletionSource? stopped;
    private WaveOutEvent? output; private AudioFileReader? reader; private string? playbackTemporary;
    public float RecordingLevel
    {
        get; private set;
    }
    public string? RecordingPath
    {
        get; private set;
    }
    public IReadOnlyList<Microphone> Microphones
    {
        get
        {
            try
            {
                return Enumerable.Range(0, WaveIn.DeviceCount).Select(i => new Microphone(i, WaveIn.GetCapabilities(i).ProductName)).ToList();
            }
            catch { return []; }
        }
    }
    public void StartRecording(int device)
    {
        if (input is not null)
            throw new InvalidOperationException("A recording is already active.");
        Directory.CreateDirectory(Workspace.Temporary);
        RecordingPath = Path.Combine(Workspace.Temporary, Guid.NewGuid().ToString("N") + ".wav");
        input = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1) };
        writer = new WaveFileWriter(RecordingPath, input.WaveFormat);
        stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        input.DataAvailable += (_, e) =>
        {
            writer?.Write(e.Buffer, 0, e.BytesRecorded);
            float peak = 0;
            for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
                peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f));
            RecordingLevel = peak;
        };
        input.RecordingStopped += (_, e) => { writer?.Dispose(); writer = null; if (e.Exception is not null) stopped.TrySetException(e.Exception); else stopped.TrySetResult(); };
        try
        {
            input.StartRecording();
        }
        catch { input.Dispose(); input = null; writer?.Dispose(); writer = null; throw; }
    }
    public async Task<string> StopRecordingAsync()
    {
        if (input is null || stopped is null || RecordingPath is null)
            throw new InvalidOperationException("No active recording.");
        input.StopRecording();
        try
        {
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return RecordingPath;
        }
        finally { input.Dispose(); input = null; writer?.Dispose(); writer = null; RecordingLevel = 0; }
    }
    public void Play(string path)
    {
        StopPlayback();
        if (path.EndsWith(".hkaudio", StringComparison.OrdinalIgnoreCase))
            path = playbackTemporary = WindowsContentProtector.DecryptRecording(path);
        reader = new AudioFileReader(path);
        output = new WaveOutEvent();
        output.Init(reader);
        output.Play();
    }
    public double Duration => reader?.TotalTime.TotalSeconds ?? 0;
    public double Position
    {
        get => reader?.CurrentTime.TotalSeconds ?? 0; set
        {
            if (reader is not null)
                reader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(value, 0, Duration));
        }
    }
    public void Pause() => output?.Pause();
    public void Resume() => output?.Play();
    public void StopPlayback()
    {
        output?.Stop();
        output?.Dispose();
        output = null;
        reader?.Dispose();
        reader = null;
        if (playbackTemporary is not null)
        {
            try
            {
                File.Delete(playbackTemporary);
            }
            catch (IOException) { }
            playbackTemporary = null;
        }
    }
    public void Dispose()
    {
        StopPlayback();
        input?.StopRecording();
        input?.Dispose();
        writer?.Dispose();
    }
}
