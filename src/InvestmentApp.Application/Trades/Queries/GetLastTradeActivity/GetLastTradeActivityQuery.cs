using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Trades.Queries.GetLastTradeActivity;

/// <summary>
/// Lệnh gần nhất của người dùng, để trang chủ đếm số ngày chưa động tay.
///
/// Không dùng lại dữ liệu vị thế đang mở: bán sạch một mã thì vị thế biến mất và lệnh đó
/// tàng hình, khiến đồng hồ kiên nhẫn nhảy vọt đúng vào lúc người dùng vừa làm việc cảm tính
/// nhất. <c>Trade</c> không mang <c>UserId</c> nên quyền sở hữu đi qua <c>Portfolio</c>.
/// </summary>
public class GetLastTradeActivityQuery : IRequest<LastTradeActivityDto>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class LastTradeActivityDto
{
    /// <summary>Null khi người dùng chưa có lệnh nào — trang chủ hiện "Chưa có lệnh nào" chứ không bịa số ngày.</summary>
    public DateTime? LastTradeDate { get; set; }

    /// <summary>Số ngày lịch VN kể từ lệnh gần nhất. Null cùng lúc với <see cref="LastTradeDate"/>.</summary>
    public int? DaysSince { get; set; }
}

public class GetLastTradeActivityQueryHandler : IRequestHandler<GetLastTradeActivityQuery, LastTradeActivityDto>
{
    private readonly IPortfolioRepository _portfolios;
    private readonly ITradeRepository _trades;

    public GetLastTradeActivityQueryHandler(IPortfolioRepository portfolios, ITradeRepository trades)
    {
        _portfolios = portfolios;
        _trades = trades;
    }

    public async Task<LastTradeActivityDto> Handle(GetLastTradeActivityQuery request, CancellationToken cancellationToken)
    {
        var portfolioIds = (await _portfolios.GetByUserIdAsync(request.UserId, cancellationToken))
            .Where(p => !p.IsDeleted)
            .Select(p => p.Id)
            .ToList();

        if (portfolioIds.Count == 0)
            return new LastTradeActivityDto();

        var lastTradeDate = await _trades.GetLastTradeDateByPortfolioIdsAsync(portfolioIds, cancellationToken);
        if (lastTradeDate is null)
            return new LastTradeActivityDto();

        return new LastTradeActivityDto
        {
            LastTradeDate = lastTradeDate,
            DaysSince = VietnamDate.DaysBetween(lastTradeDate.Value, DateTime.UtcNow),
        };
    }
}
