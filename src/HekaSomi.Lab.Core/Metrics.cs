using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HekaSomi.Lab.Core;

public static partial class Metrics
{
    [GeneratedRegex(@"[^\p{L}\p{M}\p{N}\s]", RegexOptions.CultureInvariant)]
    private static partial Regex Punctuation();
    public static string Normalize(string value) => Punctuation().Replace(value.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), " ");
    public static string[] Words(string value) => Normalize(value).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    public static EditScore Wer(string reference, string hypothesis) => Distance(Words(reference), Words(hypothesis));
    public static EditScore Cer(string reference, string hypothesis) => Distance(
        Normalize(reference).EnumerateRunes().Where(r => !Rune.IsWhiteSpace(r)).Select(r => r.ToString()).ToArray(),
        Normalize(hypothesis).EnumerateRunes().Where(r => !Rune.IsWhiteSpace(r)).Select(r => r.ToString()).ToArray());
    public static EditScore Distance(string[] reference, string[] hypothesis)
    {
        // Keep one row: long recordings must not allocate a quadratic matrix.
        var row = Enumerable.Range(0, hypothesis.Length + 1).Select(j => new EditScore(0, 0, j, reference.Length)).ToArray();
        for (var i = 1; i <= reference.Length; i++)
        {
            var diagonal = row[0];
            row[0] = new(0, i, 0, reference.Length);
            for (var j = 1; j <= hypothesis.Length; j++)
            {
                var above = row[j];
                if (reference[i - 1] == hypothesis[j - 1])
                    row[j] = diagonal;
                else
                {
                    var substitution = diagonal with
                    {
                        Substitutions = diagonal.Substitutions + 1
                    };
                    var deletion = above with
                    {
                        Deletions = above.Deletions + 1
                    };
                    var insertion = row[j - 1] with
                    {
                        Insertions = row[j - 1].Insertions + 1
                    };
                    row[j] = new[] { substitution, deletion, insertion }.MinBy(x => x.Errors)!;
                }
                diagonal = above;
            }
        }
        return row[^1];
    }
    public static double? AggregateWer(IEnumerable<CaseResult> results)
    {
        var scores = results.Where(x => x.WordScore is not null).Select(x => x.WordScore!).ToArray();
        var words = scores.Sum(x => x.ReferenceUnits);
        return words == 0 ? null : (double)scores.Sum(x => x.Errors) / words;
    }
    public static double? Percentile(IEnumerable<double> numbers, double quantile)
    {
        if (quantile is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(quantile));
        var values = numbers.Order().ToArray();
        if (values.Length == 0)
            return null;
        var location = (values.Length - 1) * quantile;
        var low = (int)Math.Floor(location);
        var high = (int)Math.Ceiling(location);
        return values[low] + (values[high] - values[low]) * (location - low);
    }
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static List<GateResult> Gates(ComparisonRun run, double maxWerIncrease = .01)
    {
        var baseline = AggregateWer(run.Results.Where(r => r.Arm == "Baseline"));
        var candidate = AggregateWer(run.Results.Where(r => r.Arm == "Candidate"));
        var errors = run.Results.Count(r => r.Error is not null);
        var complete = run.State == "Completed" && errors == 0;
        return [
            new("Execution", complete ? GateStatus.Pass : GateStatus.Fail, $"{run.State}; {errors} case/arm errors."),
            new("WER regression (descriptive)", !complete || baseline is null || candidate is null ? GateStatus.Inconclusive : candidate - baseline <= maxWerIncrease ? GateStatus.Pass : GateStatus.Fail,
                baseline is null || candidate is null ? "Reference words or completed outputs are missing." : $"Change {(candidate - baseline):P2}; permitted increase {maxWerIncrease:P2}. No inferential release claim."),
            new("Translation meaning", GateStatus.Inconclusive, "Independent bilingual adequacy and critical-error review required."),
            new("Behavioral validity", GateStatus.Inconclusive, "Independent owner study and retest required."),
            new("Live caption latency", GateStatus.NotApplicable, "This runner measures offline requests, not live caption rendering.")
        ];
    }
}
