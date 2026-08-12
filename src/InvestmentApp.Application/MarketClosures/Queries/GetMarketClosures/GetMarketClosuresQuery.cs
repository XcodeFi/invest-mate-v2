using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;

public record GetMarketClosuresQuery(string UserId, int Year) : IRequest<MarketClosureYearDto>;

public record MarketClosureYearDto(int Year, List<MarketClosureMonthDto> Months);

public record MarketClosureMonthDto(int Month, List<MarketClosureDayDto> Days);

/// <summary>Ghi chú ở cấp NGÀY: một tháng có thể chứa hai đợt lễ khác nhau (4/2026).</summary>
public record MarketClosureDayDto(int Day, string? Note);

public class GetMarketClosuresQueryHandler : IRequestHandler<GetMarketClosuresQuery, MarketClosureYearDto>
{
    private readonly IMarketClosureRepository _repository;

    public GetMarketClosuresQueryHandler(IMarketClosureRepository repository)
        => _repository = repository;

    public async Task<MarketClosureYearDto> Handle(
        GetMarketClosuresQuery request, CancellationToken cancellationToken)
    {
        var from = new DateTime(request.Year, 1, 1);
        var to = new DateTime(request.Year, 12, 31);

        var closures = await _repository.GetByUserAndRangeAsync(
            request.UserId, from, to, cancellationToken);

        var months = closures
            .GroupBy(c => c.Date.Month)
            .OrderBy(g => g.Key)
            .Select(g => new MarketClosureMonthDto(
                g.Key,
                g.OrderBy(c => c.Date)
                    .Select(c => new MarketClosureDayDto(c.Date.Day, c.Note))
                    .ToList()))
            .ToList();

        return new MarketClosureYearDto(request.Year, months);
    }
}
