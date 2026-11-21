using HekaSomi.Lab.Core;
using System.Net;
using System.Text;

namespace HekaSomi.Lab.Infrastructure;

public static class DatasetFiles
{
    public static async Task<Dataset> LoadAsync(string path)
    {
        var dataset = JsonDefaults.Read<Dataset>(await File.ReadAllTextAsync(path));
        if (dataset.SchemaVersion != 1)
            throw new InvalidDataException("Unsupported dataset schema version.");
        if (dataset.Cases.Count > 1000)
            throw new InvalidDataException("Split datasets larger than 1,000 cases.");
        if (dataset.Cases.Select(c => c.Id).Distinct().Count() != dataset.Cases.Count)
            throw new InvalidDataException("Dataset contains duplicate case IDs.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        return dataset with
        {
            Cases = dataset.Cases.Select(c => c with { AudioPath = string.IsNullOrWhiteSpace(c.AudioPath) ? null : Path.GetFullPath(c.AudioPath, directory) }).ToList()
        };
    }
    public static Task SaveAsync(string path, Dataset dataset) => File.WriteAllTextAsync(path, JsonDefaults.Write(dataset));
}
public static class RunReports
{
    public static string CsvCell(string? value)
    {
        value ??= "";
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') || value.StartsWith('\t') || value.StartsWith('\r'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    public static string Csv(ComparisonRun run)
    {
        var builder = new StringBuilder("Case,Arm,Transcript,Translation,WER,CER,OfflineRequestMs,Evidence,Error\r\n");
        foreach (var row in run.Results)
            builder.AppendLine(string.Join(",", new[] { row.CaseName, row.Arm, row.Output?.Transcript, row.Output?.Translation,
            row.WordScore?.Rate?.ToString("R", System.Globalization.CultureInfo.InvariantCulture), row.CharacterScore?.Rate?.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            row.Output?.TotalMs?.ToString("R", System.Globalization.CultureInfo.InvariantCulture), row.Output?.EvidenceKind, row.Error }.Select(CsvCell)));
        return builder.ToString();
    }
    public static string Html(ComparisonRun run)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "—");
        var rows = string.Join("", run.Results.Select(r => $"<tr><td>{E(r.CaseName)}<small>{E(r.Arm)}</small></td><td>{E(r.ReferenceTranscript)}<hr>{E(r.Output?.Transcript)}</td><td>{E(r.Output?.Translation)}</td><td>{E(r.WordScore?.Rate?.ToString("P2"))}</td><td>{E(r.Output?.TotalMs?.ToString("0") is string ms ? ms + " ms" : null)}<small>{E(r.Output?.EvidenceKind ?? r.Error)}</small></td></tr>"));
        var gates = string.Join("", run.Gates.Select(g => $"<li><strong>{E(g.Metric)} · {E(g.Status.ToString())}</strong><br>{E(g.Detail)}</li>"));
        return $$"""
        <!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>HekaSomi · {{E(run.DatasetName)}}</title>
        <style>body{font:15px/1.6 system-ui;margin:0;background:#f4f6f8;color:#132932}main{max-width:1150px;margin:48px auto;padding:32px;background:white;border-radius:16px}h1{font-size:34px}h2{margin-top:36px}small{display:block;color:#637480}table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:14px;border-bottom:1px solid #dae3e7;vertical-align:top}th{background:#eaf2ef}li{margin:12px 0}code{overflow-wrap:anywhere}hr{border:0;border-top:1px solid #ddd}</style>
        <main><small>HEKASOMI / TRANSLATION LAB</small><h1>{{E(run.DatasetName)}}</h1><p>{{E(run.StartedAt.ToString("u"))}} · {{E(run.State)}}</p>
        <p>Baseline: {{E(run.Baseline.Name)}} ({{E(run.Baseline.Kind.ToString())}}) · Candidate: {{E(run.Candidate.Name)}} ({{E(run.Candidate.Kind.ToString())}})</p>
        <p>{{E(run.Environment)}}<br>Dataset SHA-256: <code>{{E(run.DatasetHash)}}</code></p>
        <p>WER uses NFKC, lowercase, punctuation-to-space and whitespace tokenization. Scores are descriptive. Replay timings are unavailable. Live request durations are offline measurements, not caption latency. Human meaning and behavioral validation remain separate.</p>
        <h2>Checks</h2><ul>{{gates}}</ul><h2>Case evidence</h2><table><thead><tr><th>Case</th><th>Reference / transcript</th><th>Translation</th><th>WER</th><th>Request</th></tr></thead><tbody>{{rows}}</tbody></table></main></html>
        """;
    }
    public static async Task ExportAsync(string directory, ComparisonRun run)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "run.json"), JsonDefaults.Write(run));
        await File.WriteAllTextAsync(Path.Combine(directory, "results.csv"), Csv(run), new UTF8Encoding(true));
        await File.WriteAllTextAsync(Path.Combine(directory, "report.html"), Html(run));
    }
}
