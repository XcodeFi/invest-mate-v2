using System.Text.Json.Serialization;
using MediatR;

namespace InvestmentApp.Application.Admin.Queries.GetUsersOverview;

public class GetUsersOverviewQuery : IRequest<UsersOverviewResult>
{
    [JsonIgnore]
    public string CallerUserId { get; set; } = null!;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class UsersOverviewResult
{
    public List<UserOverviewDto> Items { get; set; } = new();
    public long TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class UserOverviewDto
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int PortfolioCount { get; set; }
    public int TradeCount { get; set; }
    public DateTime? LastTradeAt { get; set; }
    public DateTime? LastImpersonatedAt { get; set; }
}
