using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;

namespace HekaSomi.Lab.Desktop.Services;

public static class UserFeedback
{
    public static string Explain(Exception error) => error switch
    {
        HttpRequestException h => h.StatusCode is { } status
            ? $"The service returned HTTP {(int)status}. Check its access, usage limits and configuration. No automatic billing fallback was used."
            : "The service could not be reached. Check the selected endpoint and connection. For local models, make sure Ollama is running.",
        JsonException => "This file or model response could not be read. Check the selected file or model and try again.",
        UnauthorizedAccessException => "Access was denied. Check the selected folder's permissions and Windows microphone privacy settings.",
        Win32Exception => "The speech engine could not start. Extract the entire Windows ZIP again, then check the speech package in Setup.",
        _ => error.Message
    };
}
