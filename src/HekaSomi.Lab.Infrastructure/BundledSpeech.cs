using System.Diagnostics;
using System.Security.Cryptography;
using HekaSomi.Lab.Core;

namespace HekaSomi.Lab.Infrastructure;

public sealed record SpeechBundleManifest(string Version, string ModelName, string Executable, string Model, Dictionary<string, string> Sha256);

public sealed record SpeechBundle(string Root, SpeechBundleManifest Manifest, string ExecutablePath, string ModelPath)
{
    public async Task VerifyAsync(CancellationToken ct)
    {
        if (Manifest.Sha256 is null || Manifest.Sha256.Count == 0 || !Manifest.Sha256.ContainsKey(Manifest.Executable) || !Manifest.Sha256.ContainsKey(Manifest.Model))
            throw new InvalidDataException("The included speech package is incomplete. Extract the complete Windows ZIP again.");
        foreach (var pair in Manifest.Sha256)
        {
            var path = BundledSpeech.ResolveChild(Root, pair.Key);
            if (!File.Exists(path))
                throw new FileNotFoundException("A speech file is missing. Extract the complete Windows ZIP again.", path);
            await using var file = File.OpenRead(path);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            if (!string.Equals(actual, pair.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("An included speech file is damaged or changed. Extract the complete Windows ZIP again.");
        }
    }
}

public static class BundledSpeech
{
    public static SpeechBundle Resolve(string applicationDirectory)
    {
        var root = Path.Combine(applicationDirectory, "assets", "whisper");
        var path = Path.Combine(root, "bundle.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("The included speech package is missing. Extract the complete Windows ZIP, or run scripts/Prepare-Speech.ps1 when building from source.");
        var manifest = JsonDefaults.Read<SpeechBundleManifest>(File.ReadAllText(path));
        return new(root, manifest, ResolveChild(root, manifest.Executable), ResolveChild(root, manifest.Model));
    }

    public static string ResolveChild(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains('\\'))
            throw new InvalidDataException("Invalid speech package path.");
        var normalizedRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(relative, root);
        if (!full.StartsWith(normalizedRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Invalid speech package path.");
        return full;
    }
}

public static class WhisperArguments
{
    public static ProcessStartInfo Create(string audioPath, EngineConfiguration config, string language, string outputPath, bool translateToEnglish = false)
    {
        var start = new ProcessStartInfo(config.WhisperExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(config.WhisperExecutable))!
        };
        var code = string.IsNullOrWhiteSpace(language) ? "auto" : language.Trim().Split('-')[0].ToLowerInvariant();
        foreach (var arg in new[] { "-m", Path.GetFullPath(config.WhisperModel), "-f", Path.GetFullPath(audioPath), "-l", code,
            "-otxt", "-of", outputPath, "-np", "-ng", "-t", Math.Clamp(Environment.ProcessorCount / 2, 1, 8).ToString(System.Globalization.CultureInfo.InvariantCulture) })
            start.ArgumentList.Add(arg);
        if (translateToEnglish)
            start.ArgumentList.Add("-tr");
        return start;
    }
}
