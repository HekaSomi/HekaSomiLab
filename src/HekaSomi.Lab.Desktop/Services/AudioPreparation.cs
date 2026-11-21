using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace HekaSomi.Lab.Desktop.Services;

public static class AudioPreparation
{
    public const string Filter = "Audio files|*.wav;*.mp3;*.m4a;*.aac;*.wma;*.aiff;*.aif;*.flac|All files|*.*";

    // Work on a normalized copy. The original recording is never modified.
    public static Task<string> PrepareAsync(string inputPath, string destinationDirectory, CancellationToken ct) => Task.Run(() =>
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Choose an existing audio file.");
        if (new FileInfo(inputPath).Length > 250L * 1024 * 1024)
            throw new InvalidDataException("Choose an audio file under 250 MB. Split longer recordings first.");
        Directory.CreateDirectory(destinationDirectory);
        var destination = Path.Combine(destinationDirectory, Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            using var source = new AudioFileReader(inputPath);
            if (source.TotalTime > TimeSpan.FromMinutes(10))
                throw new InvalidDataException("Use clips up to 10 minutes. Split longer recordings first.");
            ISampleProvider mono = source;
            if (source.WaveFormat.Channels == 2)
                mono = new StereoToMonoSampleProvider(source) { LeftVolume = .5f, RightVolume = .5f };
            else if (source.WaveFormat.Channels != 1)
                throw new InvalidDataException("Use mono or stereo audio.");
            var pcm = new SampleToWaveProvider16(new WdlResamplingSampleProvider(mono, 16000));
            using (var writer = new WaveFileWriter(destination, pcm.WaveFormat))
            {
                var bytes = new byte[32000];
                int count;
                long total = 0;
                while ((count = pcm.Read(bytes, 0, bytes.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    total += count;
                    if (total > 32000L * 600)
                        throw new InvalidDataException("Use clips up to 10 minutes.");
                    writer.Write(bytes, 0, count);
                }
                if (total == 0)
                    throw new InvalidDataException("This file contains no audio.");
            }
            return destination;
        }
        catch { File.Delete(destination); throw; }
    }, ct);
}
