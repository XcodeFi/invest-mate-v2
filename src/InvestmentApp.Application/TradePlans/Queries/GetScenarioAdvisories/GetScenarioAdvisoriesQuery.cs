using InvestmentApp.Application.Common.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetScenarioAdvisories;

public class GetScenarioAdvisoriesQuery : IRequest<List<ScenarioAdvisory>>
{
    public string UserId { get; set; } = null!;
}

public class GetScenarioAdvisoriesQueryHandler : IRequestHandler<GetScenarioAdvisoriesQuery, List<ScenarioAdvisory>>
{
    private readonly IScenarioAdvisoryService _advisoryService;

    public GetScenarioAdvisoriesQueryHandler(IScenarioAdvisoryService advisoryService)
    {
        _advisoryService = advisoryService;
    }

    public Task<List<ScenarioAdvisory>> Handle(GetScenarioAdvisoriesQuery request, CancellationToken cancellationToken)
    {
        return _advisoryService.GetAdvisoriesAsync(request.UserId, cancellationToken);
    }
}
