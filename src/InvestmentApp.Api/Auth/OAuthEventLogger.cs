using Microsoft.AspNetCore.Authentication;

namespace InvestmentApp.Api.Auth;

/// <summary>
/// Captures OAuth middleware-level failures that would otherwise vanish silently into a
/// generic 500 with no app log. Wired via
/// <c>options.Events.OnRemoteFailure = ctx =&gt; OAuthEventLogger.LogRemoteFailure(...)</c>
/// inside <c>AddGoogle(...)</c>; the controller's own try-catch never runs when the
/// failure is upstream of the controller (correlation cookie missing, state mismatch,
/// token exchange rejected, etc.).
///
/// Bug B audit (2026-04-26): two 500s on /api/v1/auth/google/callback with no stack
/// trace because no event handler was wired.
/// </summary>
public static class OAuthEventLogger
{
    private const int MaxMessageLength = 500;

    public static void LogRemoteFailure(
        ILogger logger,
        string path,
        string correlationId,
        Exception? failure)
    {
        var failureType = failure?.GetType().Name ?? "(no exception)";
        var failureMessage = failure?.Message ?? "(no exception)";

        if (failureMessage.Length > MaxMessageLength)
            failureMessage = failureMessage[..MaxMessageLength] + "…";

        logger.LogError(
            failure,
            "Google OAuth remote failure at {Path} (correlationId={CorrelationId}, type={FailureType}): {FailureMessage}",
            path, correlationId, failureType, failureMessage);
    }

    /// <summary>
    /// Default event handler — call this from the OnRemoteFailure delegate so the
    /// failure is logged with all available context, then let the framework
    /// continue (which renders the configured AccessDeniedPath / 500).
    /// </summary>
    public static Task HandleRemoteFailureAsync(RemoteFailureContext context, ILogger logger)
    {
        LogRemoteFailure(
            logger,
            path: context.Request.Path.Value ?? "(unknown path)",
            correlationId: context.HttpContext.TraceIdentifier,
            failure: context.Failure);
        return Task.CompletedTask;
    }
}
