namespace HekaSomi.Lab.Core;

public interface ITranslationEngine
{
    Task<EngineOutput> ProcessAsync(AudioCase sample, Dataset dataset, EngineConfiguration configuration, string arm,
        string? translationStyle, CancellationToken cancellationToken);
}
public sealed class BenchmarkRunner(ITranslationEngine engine)
{
    public async Task<ComparisonRun> RunAsync(Dataset dataset, EngineConfiguration baseline, EngineConfiguration candidate,
        PersonalityConstruct? construct = null, IProgress<RunProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (dataset.Cases.Count == 0)
            throw new InvalidDataException("Add at least one case.");
        if (dataset.Cases.Select(c => c.Id).Distinct().Count() != dataset.Cases.Count)
            throw new InvalidDataException("Case IDs must be unique.");
        if ((baseline.UsePersonality || candidate.UsePersonality) && construct?.Status != ConstructStatus.Approved)
            throw new InvalidOperationException("Select an approved construct before using personality.");
        var audioHashes = new Dictionary<string, string>();
        foreach (var sample in dataset.Cases.Where(c => c.AudioPath is not null))
        {
            try
            {
                await using var file = File.OpenRead(sample.AudioPath!);
                audioHashes[sample.Id] = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(file, cancellationToken)).ToLowerInvariant();
            }
            catch (IOException) { audioHashes[sample.Id] = "unavailable"; }
            catch (UnauthorizedAccessException) { audioHashes[sample.Id] = "unavailable"; }
        }
        // Audio bytes and textual evidence affect the hash; machine-specific absolute paths do not.
        var fingerprint = new
        {
            dataset = dataset with
            {
                Cases = dataset.Cases.Select(c => c with { AudioPath = null }).ToList()
            },
            audioHashes
        };
        var run = new ComparisonRun
        {
            DatasetName = dataset.Name,
            DatasetHash = Metrics.Hash(JsonDefaults.Write(fingerprint)),
            AudioHashes = audioHashes,
            Baseline = baseline,
            Candidate = candidate,
            ConstructId = construct?.Id,
            ConstructVersion = construct?.Version,
            ConstructHash = construct is null ? null : Metrics.Hash(JsonDefaults.Write(construct))
        };
        var total = dataset.Cases.Count * 2;
        try
        {
            for (var index = 0; index < dataset.Cases.Count; index++)
            {
                var sample = dataset.Cases[index];
                // Alternate order to reduce systematic A/B ordering effects.
                var configs = index % 2 == 0 ? new[] { ("Baseline", baseline), ("Candidate", candidate) } : [("Candidate", candidate), ("Baseline", baseline)];
                foreach (var (arm, config) in configs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new(run.Results.Count, total, $"{arm} · {sample.Name}"));
                    try
                    {
                        var style = config.UsePersonality ? ConstructCompiler.ForTranslation(construct!) : null;
                        var output = await engine.ProcessAsync(sample, dataset, config, arm, style, cancellationToken);
                        run.Results.Add(new(sample.Id, sample.Name, arm, sample.ReferenceTranscript, sample.ReferenceTranslation, output,
                            config.Kind == EngineKind.TextAndOllama ? null : Metrics.Wer(sample.ReferenceTranscript, output.Transcript), config.Kind == EngineKind.TextAndOllama ? null : Metrics.Cer(sample.ReferenceTranscript, output.Transcript), null));
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { run.Results.Add(new(sample.Id, sample.Name, arm, sample.ReferenceTranscript, sample.ReferenceTranslation, null, null, null, ex.Message)); }
                }
            }
            run.State = "Completed";
        }
        catch (OperationCanceledException) { run.State = "Canceled"; }
        run.Gates = Metrics.Gates(run);
        progress?.Report(new(run.Results.Count, total, $"{run.State} · {run.Results.Count}/{total} outputs retained"));
        return run;
    }
}
