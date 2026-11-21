using HekaSomi.Lab.Core;
using System.Net.Http.Headers;

namespace HekaSomi.Lab.Infrastructure;

public sealed class PlatformClient(HttpClient http, string baseUrl, string token)
{
    private Uri Endpoint(string route)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var u) || u.Scheme != "https" || u.UserInfo.Length > 0 || u.Query.Length > 0 || u.Fragment.Length > 0)
            throw new InvalidOperationException("Use the HTTPS HekaSomi platform address without query parameters or credentials.");
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Set HEKASOMI_LAB_TOKEN to an authorized, read-only Lab access token, then restart Lab.");
        return new Uri(baseUrl.TrimEnd('/') + "/" + route);
    }
    private async Task<T> Get<T>(string route, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Endpoint(route));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Platform returned HTTP {(int)response.StatusCode}; no platform data was changed.", null, response.StatusCode);
        return JsonDefaults.Read<T>(await BoundedJson.ReadAsync(response.Content, 8 * 1024 * 1024, ct));
    }
    public async Task<PlatformReport> FetchReportAsync(CancellationToken ct)
    {
        var report = await Get<PlatformReport>("api/lab/v1/report", ct); report.Validate(); return report;
    }
    public async Task<GlobalCohort> FetchCohortAsync(AssessmentSession s, ParticipationRun r, CancellationToken ct)
    {
        var query = new Dictionary<string, string> { ["testHash"] = s.Test.Hash, ["provider"] = r.Connection.Provider,
            ["model"] = r.Connection.Model, ["connectionIdentity"] = r.Connection.Identity, ["condition"] = r.Condition.ToString(), ["compilerVersion"] = r.CompilerVersion,
            ["language"] = s.Test.Language, ["evidenceKind"] = r.EvidenceKind, ["policyVersion"] = ParticipationScoring.PolicyVersion };
        var route = "api/lab/v1/cohort?" + string.Join("&", query.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
        var cohort = await Get<GlobalCohort>(route, ct); cohort.ValidateFor(s, r); return cohort;
    }
}
