using FluentAssertions;
using InvestmentApp.Api.Auth;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestmentApp.Api.Tests.Auth;

/// <summary>
/// Bug B (audit 2026-04-26): two 500s on /api/v1/auth/google/callback with no
/// stack trace in app logs because the failure happens inside the Google OAuth
/// middleware, before the controller's try-catch runs. Wiring OnRemoteFailure
/// is what gets the exception logged — these tests lock that the helper produces
/// the structured fields ops can grep for (correlationId, failure type, path).
/// </summary>
public class OAuthEventLoggerTests
{
    private readonly Mock<ILogger> _logger = new();

    [Fact]
    public void LogRemoteFailure_WithException_LogsAtErrorWithStructuredFields()
    {
        var failure = new InvalidOperationException("Correlation cookie not found");

        OAuthEventLogger.LogRemoteFailure(
            _logger.Object,
            path: "/api/v1/auth/google/callback",
            correlationId: "trace-abc-123",
            failure: failure);

        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("/api/v1/auth/google/callback")
                                       && v.ToString()!.Contains("trace-abc-123")
                                       && v.ToString()!.Contains("InvalidOperationException")
                                       && v.ToString()!.Contains("Correlation cookie not found")),
            failure,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogRemoteFailure_NullException_StillLogs_WithFallbackType()
    {
        OAuthEventLogger.LogRemoteFailure(
            _logger.Object,
            path: "/api/v1/auth/google/callback",
            correlationId: "trace-xyz",
            failure: null);

        // Even when middleware sets ctx.Failure=null (rare but possible), we still log
        // so ops know "something failed at OAuth callback" — better than silence.
        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("trace-xyz")
                                       && v.ToString()!.Contains("(no exception)")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogRemoteFailure_TruncatesLongMessages_To500Chars()
    {
        // Defense against log spam from a runaway exception message
        var longMessage = new string('x', 2000);
        var failure = new InvalidOperationException(longMessage);

        OAuthEventLogger.LogRemoteFailure(
            _logger.Object,
            path: "/api/v1/auth/google/callback",
            correlationId: "trace-trunc",
            failure: failure);

        _logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) =>
                v.ToString()!.Length < 1000  // log line stays bounded
                && v.ToString()!.Contains("…")),  // truncation marker present
            failure,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
