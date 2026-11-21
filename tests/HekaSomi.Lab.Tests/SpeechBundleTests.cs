using System.Security.Cryptography;
using HekaSomi.Lab.Core;
using HekaSomi.Lab.Infrastructure;
using Xunit;
namespace HekaSomi.Lab.Tests;

public class SpeechBundleTests
{
    private static SpeechBundle Create(string app)
    {
        var root = Path.Combine(app, "assets", "whisper");
        Directory.CreateDirectory(Path.Combine(root, "windows-x64"));
        Directory.CreateDirectory(Path.Combine(root, "models"));
        File.WriteAllText(Path.Combine(root, "windows-x64", "whisper-cli.exe"), "test engine bytes");
        File.WriteAllText(Path.Combine(root, "models", "model.bin"), "test model bytes");
        var hashes = new Dictionary<string, string>();
        foreach (var path in new[] { "windows-x64/whisper-cli.exe", "models/model.bin" })
            hashes[path] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path))));
        var manifest = new SpeechBundleManifest("test-1", "Test model", "windows-x64/whisper-cli.exe", "models/model.bin", hashes);
        File.WriteAllText(Path.Combine(root, "bundle.json"), JsonDefaults.Write(manifest));
        return BundledSpeech.Resolve(app);
    }
    [Fact]
    public async Task CompleteBundleVerifies()
    {
        using var dir = new TemporaryDirectory();
        var bundle = Create(dir.Path);
        await bundle.VerifyAsync(CancellationToken.None);
        Assert.Equal("Test model", bundle.Manifest.ModelName);
        Assert.True(File.Exists(bundle.ExecutablePath));
    }
    [Fact]
    public async Task ChangedModelIsRejected()
    {
        using var dir = new TemporaryDirectory(); var bundle = Create(dir.Path);
        await File.WriteAllTextAsync(bundle.ModelPath, "corruption");
        await Assert.ThrowsAsync<InvalidDataException>(() => bundle.VerifyAsync(CancellationToken.None));
    }
    [Fact]
    public async Task MissingEngineIsRejected()
    {
        using var dir = new TemporaryDirectory(); var bundle = Create(dir.Path);
        File.Delete(bundle.ExecutablePath);
        await Assert.ThrowsAsync<FileNotFoundException>(() => bundle.VerifyAsync(CancellationToken.None));
    }
    [Fact]
    public void MissingManifestExplainsRecovery()
    {
        using var dir = new TemporaryDirectory();
        var error = Assert.Throws<FileNotFoundException>(() => BundledSpeech.Resolve(dir.Path));
        Assert.Contains("complete Windows ZIP", error.Message);
    }
    [Theory]
    [InlineData("../outside")]
    [InlineData("/outside")]
    [InlineData("..\\outside")]
    [InlineData("")]
    public void BundlePathsStayWithinPackage(string path)
    {
        using var dir = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => BundledSpeech.ResolveChild(dir.Path, path));
    }
    [Fact]
    public async Task VerificationObservesCancellation()
    {
        using var dir = new TemporaryDirectory(); var bundle = Create(dir.Path);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bundle.VerifyAsync(canceled.Token));
    }
    [Fact]
    public void ArgumentsKeepPathsWithSpacesAndNormalizeLanguage()
    {
        var config = new EngineConfiguration { WhisperExecutable = Path.GetFullPath("speech files/engine.exe"), WhisperModel = Path.GetFullPath("speech files/model.bin") };
        var audio = Path.GetFullPath("my recordings/voice.wav"); var output = Path.GetFullPath("my output/transcript");
        var start = WhisperArguments.Create(audio, config, " EN-US ", output);
        Assert.False(start.UseShellExecute);
        Assert.Contains(audio, start.ArgumentList); Assert.Contains(config.WhisperModel, start.ArgumentList);
        Assert.Contains(output, start.ArgumentList); Assert.Contains("en", start.ArgumentList);
        Assert.DoesNotContain("-tr", start.ArgumentList);
    }
    [Fact]
    public void EnglishTranslationIsExplicit()
    {
        var config = new EngineConfiguration { WhisperExecutable = Path.GetFullPath("engine.exe"), WhisperModel = Path.GetFullPath("model.bin") };
        var start = WhisperArguments.Create("voice.wav", config, "", "out", translateToEnglish: true);
        Assert.Contains("auto", start.ArgumentList); Assert.Contains("-tr", start.ArgumentList);
    }
    [Fact]
    public async Task EmptyHashListCannotBypassVerification()
    {
        using var dir = new TemporaryDirectory(); var bundle = Create(dir.Path);
        bundle.Manifest.Sha256.Clear();
        await Assert.ThrowsAsync<InvalidDataException>(() => bundle.VerifyAsync(CancellationToken.None));
    }
}
