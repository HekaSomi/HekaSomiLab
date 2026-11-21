using System.Text;
namespace HekaSomi.Lab.Core;

public record WaveInfo(int SampleRate, int Channels, int BitsPerSample, double DurationSeconds, double[] Peaks, double ClippedFraction, double Rms);
public static class WaveFile
{
    public static WaveInfo Inspect(string path, int buckets = 160)
    {
        if (buckets is < 1 or > 10000)
            throw new ArgumentOutOfRangeException(nameof(buckets));
        using var stream = File.OpenRead(path);
        if (stream.Length > 100 * 1024 * 1024)
            throw new InvalidDataException("WAV files must be smaller than 100 MB.");
        using var reader = new BinaryReader(stream, Encoding.ASCII);
        string Four() => Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (Four() != "RIFF")
            throw new InvalidDataException("Use an uncompressed PCM WAV file.");
        reader.ReadUInt32();
        if (Four() != "WAVE")
            throw new InvalidDataException("Invalid WAV header.");
        ushort format = 0, channels = 0, bits = 0;
        int rate = 0;
        byte[]? audio = null;
        while (stream.Position + 8 <= stream.Length)
        {
            var kind = Four();
            var length = reader.ReadUInt32();
            if (length > stream.Length - stream.Position)
                throw new InvalidDataException("Truncated WAV chunk.");
            var next = stream.Position + length;
            if (kind == "fmt ")
            {
                if (length < 16)
                    throw new InvalidDataException("Invalid WAV format chunk.");
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                rate = reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadUInt16();
                bits = reader.ReadUInt16();
            }
            else if (kind == "data")
                audio = reader.ReadBytes(checked((int)length));
            stream.Position = Math.Min(next + length % 2, stream.Length);
        }
        if (format != 1 || bits != 16 || channels is < 1 or > 8 || rate < 8000 || rate > 192000 || audio is null || audio.Length == 0)
            throw new InvalidDataException("Supported input: 16-bit PCM WAV, 8–192 kHz, 1–8 channels. Convert compressed audio before importing.");
        if (audio.Length % (channels * 2) != 0)
            throw new InvalidDataException("Incomplete audio frame.");
        var frames = audio.Length / (channels * 2);
        var peaks = new double[buckets];
        double squares = 0;
        var clipped = 0;
        for (var f = 0; f < frames; f++)
        {
            double sum = 0;
            for (var c = 0; c < channels; c++)
                sum += BitConverter.ToInt16(audio, (f * channels + c) * 2) / 32768.0;
            var value = sum / channels;
            var peak = Math.Abs(value);
            squares += value * value;
            if (peak >= .999)
                clipped++;
            var bucket = Math.Min(buckets - 1, (int)((long)f * buckets / frames));
            peaks[bucket] = Math.Max(peaks[bucket], peak);
        }
        return new(rate, channels, bits, (double)frames / rate, peaks, (double)clipped / frames, Math.Sqrt(squares / frames));
    }
}
