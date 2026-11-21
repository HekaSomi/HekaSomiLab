namespace HekaSomi.Lab.Core;

// Input must come from the platform's authorized, consent-filtered database. Never promote desktop file imports to verified contributions.
public sealed record CohortContribution(string OwnerKey, EvaluationObservation Observation, bool AnalyticsConsent, bool RevokedOrDeleted);
public static class ParticipationCohorts
{
    public static GlobalCohort Build(AssessmentSession selected, ParticipationRun run, IEnumerable<CohortContribution> contributions, DateTimeOffset asOf, int minimumOwners = 10)
    {
        if (minimumOwners < 10) throw new ArgumentOutOfRangeException(nameof(minimumOwners));
        var cohort = contributions.Where(c => c.AnalyticsConsent && !c.RevokedOrDeleted && !string.IsNullOrWhiteSpace(c.OwnerKey))
            .Where(c => c.Observation.Session.Test.Hash == selected.Test.Hash
                && c.Observation.Run.Connection.Provider == run.Connection.Provider
                && c.Observation.Run.Connection.Model == run.Connection.Model
                && c.Observation.Run.Connection.Identity == run.Connection.Identity
                && c.Observation.Run.Condition == run.Condition
                && c.Observation.Run.CompilerVersion == run.CompilerVersion
                && c.Observation.Run.EvidenceKind == run.EvidenceKind)
            .Select(c => new { c.OwnerKey, c.Observation, Score = ParticipationScoring.Score(c.Observation.Session, c.Observation.Run, c.Observation.Responses) })
            .Where(c => c.Score.Completed == c.Score.Eligible)
            // Prespecified first completed attempt per owner/cohort avoids weighting prolific users or selecting their best result.
            .GroupBy(c => c.OwnerKey).Select(g => g.OrderBy(c => c.Observation.Run.CreatedAt).ThenBy(c => c.Observation.Run.Id, StringComparer.Ordinal).First()).ToArray();
        var suppressed = cohort.Length < minimumOwners;
        return new GlobalCohort { TestHash = selected.Test.Hash, Provider = run.Connection.Provider, Model = run.Connection.Model, ConnectionIdentity = run.Connection.Identity,
            Condition = run.Condition, CompilerVersion = run.CompilerVersion, Language = selected.Test.Language, EvidenceKind = run.EvidenceKind,
            DistinctOwners = cohort.Length, MinimumOwners = minimumOwners, Suppressed = suppressed, AsOf = asOf,
            MeanMatchPercent = suppressed ? null : cohort.Average(c => c.Score.MatchPercent!.Value),
            MeanCoveragePercent = suppressed ? null : cohort.Average(c => c.Score.CoveragePercent!.Value) };
    }
}
