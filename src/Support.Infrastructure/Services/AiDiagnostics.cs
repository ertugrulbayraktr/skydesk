using System.Diagnostics;

namespace Support.Infrastructure.Services;

/// <summary>
/// Shared ActivitySource for AI spans — wired into OpenTelemetry tracing so
/// Gemini latency shows up inside the request trace.
/// </summary>
public static class AiDiagnostics
{
    public const string SourceName = "Skydesk.AI";
    public static readonly ActivitySource Source = new(SourceName);
}
