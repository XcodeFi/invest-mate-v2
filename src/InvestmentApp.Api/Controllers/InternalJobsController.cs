using InvestmentApp.Api.Auth;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>
/// Endpoints invoked by Cloud Scheduler on a cron schedule. All endpoints require an OIDC
/// ID token from a Google service account in the <c>Jobs:AllowedSchedulerSAs</c> allowlist.
///
/// Idempotent — calling any endpoint twice in quick succession just re-runs the underlying
/// job (snapshots dedupe by date, prices upsert by symbol+date).
/// </summary>
[ApiController]
[Route("internal/jobs")]
[Authorize(AuthenticationSchemes = GcpOidcExtensions.SchemeName, Policy = GcpOidcExtensions.PolicyName)]
public class InternalJobsController : ControllerBase
{
    private readonly IPriceSnapshotJobService _priceJob;
    private readonly ISnapshotService _snapshotService;
    private readonly ICurrencyService _currencyService;
    private readonly IScenarioEvaluationService _scenarioService;
    private readonly ILogger<InternalJobsController> _logger;

    public InternalJobsController(
        IPriceSnapshotJobService priceJob,
        ISnapshotService snapshotService,
        ICurrencyService currencyService,
        IScenarioEvaluationService scenarioService,
        ILogger<InternalJobsController> logger)
    {
        _priceJob = priceJob;
        _snapshotService = snapshotService;
        _currencyService = currencyService;
        _scenarioService = scenarioService;
        _logger = logger;
    }

    [HttpPost("prices")]
    public async Task<IActionResult> RunPrices(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InternalJobs.prices triggered");
        var result = await _priceJob.RunAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("snapshot")]
    public async Task<IActionResult> RunSnapshot(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InternalJobs.snapshot triggered");
        await _snapshotService.TakeAllSnapshotsAsync(cancellationToken);
        return Ok(new { Status = "ok" });
    }

    [HttpPost("exchange-rate")]
    public async Task<IActionResult> RunExchangeRate(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InternalJobs.exchange-rate triggered");
        await _currencyService.RefreshRatesAsync(cancellationToken);
        return Ok(new { Status = "ok" });
    }

    [HttpPost("scenario-eval")]
    public async Task<IActionResult> RunScenarioEval(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InternalJobs.scenario-eval triggered");
        var triggered = await _scenarioService.EvaluateAllAsync(cancellationToken);
        return Ok(new { TriggeredCount = triggered.Count });
    }
}
