using System.Text.Json;
using System.Text.Json.Serialization;

namespace HekaSomi.Lab.Core;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException("The document is empty.");
}

public enum EngineKind
{
    Replay, TextAndOllama, WhisperAndOllama
}
public enum ConstructStatus
{
    Draft, Approved, Revoked
}
public enum PersonaCondition
{
    Neutral, StyleOnly, InterviewOnly, QuestionnaireOnly, Combined
}
public enum GateStatus
{
    Pass, Fail, Inconclusive, NotApplicable
}
public record ReplayOutput(string Transcript, string Translation);
public sealed record AudioCase
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "New sample";
    public string? AudioPath
    {
        get; init;
    }
    public string? TextInput
    {
        get; init;
    }
    public string ReferenceTranscript { get; init; } = "";
    public string ReferenceTranslation { get; init; } = "";
    public ReplayOutput? BaselineReplay
    {
        get; init;
    }
    public ReplayOutput? CandidateReplay
    {
        get; init;
    }
}
public sealed record Dataset
{
    public int SchemaVersion { get; init; } = 1;
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Untitled dataset";
    public string SourceLanguage { get; init; } = "en";
    public string TargetLanguage { get; init; } = "Spanish";
    public List<AudioCase> Cases { get; init; } = [];
}
public sealed record EngineConfiguration
{
    public string Name { get; init; } = "Baseline";
    public EngineKind Kind { get; init; } = EngineKind.Replay;
    public string OllamaUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "";
    public string WhisperExecutable { get; init; } = "";
    public string WhisperModel { get; init; } = "";
    public bool UsePersonality
    {
        get; init;
    }
    public int TimeoutSeconds { get; init; } = 180;
    public double Temperature
    {
        get; init;
    }
}
public record EngineOutput(string Transcript, string Translation, double? AsrMs, double? TranslationMs, double? TotalMs, string EvidenceKind);
public record EditScore(int Substitutions, int Deletions, int Insertions, int ReferenceUnits)
{
    public int Errors => Substitutions + Deletions + Insertions;
    public double? Rate => ReferenceUnits == 0 ? null : (double)Errors / ReferenceUnits;
}
public record CaseResult(string CaseId, string CaseName, string Arm, string ReferenceTranscript, string ReferenceTranslation,
    EngineOutput? Output, EditScore? WordScore, EditScore? CharacterScore, string? Error);
public record GateResult(string Metric, GateStatus Status, string Detail);
public sealed record ComparisonRun
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public string DatasetName { get; init; } = "";
    public string DatasetHash { get; init; } = "";
    public EngineConfiguration Baseline { get; init; } = new();
    public EngineConfiguration Candidate { get; init; } = new();
    public string? ConstructId
    {
        get; init;
    }
    public int? ConstructVersion
    {
        get; init;
    }
    public string? ConstructHash
    {
        get; init;
    }
    public Dictionary<string, string> AudioHashes { get; init; } = [];
    public string Environment { get; init; } = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}; .NET {System.Environment.Version}; {System.Environment.ProcessorCount} logical CPUs";
    public string State { get; set; } = "Running";
    public List<CaseResult> Results { get; init; } = [];
    public List<GateResult> Gates { get; set; } = [];
}
public record RunProgress(int Completed, int Total, string Message);
public record EvidenceEntry(string Id, string ModuleId, string Prompt, string Text, DateTimeOffset CapturedAt, string? AudioPath = null);
public sealed record BehaviorClaim
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Statement { get; set; } = "";
    public string Category { get; set; } = "Communication";
    public string Context { get; set; } = "";
    public string SourceId { get; set; } = "";
    public bool Approved
    {
        get; set;
    }
    public bool MayDiscloseStory
    {
        get; set;
    }
}
public sealed record PersonalityConstruct
{
    public int SchemaVersion { get; init; } = 2;
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public int Version { get; init; } = 1;
    public string OwnerId { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New construct";
    public ConstructStatus Status { get; set; } = ConstructStatus.Draft;
    public string ProtocolVersion { get; init; } = "hekasomi-interview-v2";
    public string InstrumentVersion { get; init; } = "ipip-big-five-factor-markers-50-en-v1";
    public string PreferredStyle { get; set; } = "Clear, natural, and faithful to the speaker's meaning.";
    public List<EvidenceEntry> Evidence { get; init; } = [];
    public List<BehaviorClaim> Claims { get; init; } = [];
    public Dictionary<int, int> Questionnaire { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConsentRecordedAt
    {
        get; set;
    }
    public DateTimeOffset? ApprovedAt
    {
        get; set;
    }
    public string Key => $"{Id}:{Version}";
}
public record ValidationStimulus(string Id, string OwnerId, string Question, List<string> Options);
public record SealedAnswers(string CaseId, string OwnerId, string T0, string? T1 = null);
public record PersonaPrediction(string CaseId, string OwnerId, string ConstructId, int ConstructVersion, string ConstructHash,
    PersonaCondition Condition, string Choice, string Model, DateTimeOffset CreatedAt);
public record ValidationSummary(PersonaCondition Condition, int Cases, int CorrectT0, int? CorrectT1, int RetestCount, int RetestAgreements)
{
    public double AccuracyT0 => Cases == 0 ? 0 : (double)CorrectT0 / Cases;
    public double? AccuracyT1 => RetestCount == 0 ? null : (double)(CorrectT1 ?? 0) / RetestCount;
    public double? RetestAgreement => RetestCount == 0 ? null : (double)RetestAgreements / RetestCount;
}
public record ReviewRating(string RunId, string CaseId, string Arm, int Adequacy, int? Style, bool CriticalMeaningError, string Notes);
public record InterviewModule(string Id, string Title, string Prompt, string FollowUp);
