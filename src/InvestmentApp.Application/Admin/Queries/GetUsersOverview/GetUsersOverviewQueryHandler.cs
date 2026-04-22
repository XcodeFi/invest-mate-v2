using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Admin.Queries.GetUsersOverview;

public class GetUsersOverviewQueryHandler : IRequestHandler<GetUsersOverviewQuery, UsersOverviewResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IImpersonationAuditRepository _auditRepository;

    public GetUsersOverviewQueryHandler(
        IUserRepository userRepository,
        IPortfolioRepository portfolioRepository,
        ITradeRepository tradeRepository,
        IImpersonationAuditRepository auditRepository)
    {
        _userRepository = userRepository;
        _portfolioRepository = portfolioRepository;
        _tradeRepository = tradeRepository;
        _auditRepository = auditRepository;
    }

    public async Task<UsersOverviewResult> Handle(GetUsersOverviewQuery request, CancellationToken ct)
    {
        var caller = await _userRepository.GetByIdAsync(request.CallerUserId, ct);
        if (caller == null || caller.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("Caller is not an admin");

        var (users, total) = await _userRepository.GetPagedAsync(request.Page, request.PageSize, ct);
        if (users.Count == 0)
        {
            return new UsersOverviewResult
            {
                Items = new List<UserOverviewDto>(),
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize,
            };
        }

        var userIds = users.Select(u => u.Id).ToList();
        var portfoliosByUser = await _portfolioRepository.GetIdsByUserIdsAsync(userIds, ct);
        var allPortfolioIds = portfoliosByUser.Values.SelectMany(x => x).Distinct().ToList();
        var tradeStats = allPortfolioIds.Count > 0
            ? await _tradeRepository.GetStatsByPortfolioIdsAsync(allPortfolioIds, ct)
            : new Dictionary<string, (int Count, DateTime? LastTradeAt)>();

        var items = new List<UserOverviewDto>(users.Count);
        foreach (var u in users)
        {
            var portfolioIds = portfoliosByUser.TryGetValue(u.Id, out var list) ? list : new List<string>();
            int tradeCount = 0;
            DateTime? lastTradeAt = null;
            foreach (var pid in portfolioIds)
            {
                if (tradeStats.TryGetValue(pid, out var stat))
                {
                    tradeCount += stat.Count;
                    if (stat.LastTradeAt.HasValue && (!lastTradeAt.HasValue || stat.LastTradeAt.Value > lastTradeAt.Value))
                        lastTradeAt = stat.LastTradeAt;
                }
            }

            var lastImpersonated = await _auditRepository.GetLatestStartedAtByTargetAsync(u.Id, ct);

            items.Add(new UserOverviewDto
            {
                Id = u.Id,
                Email = u.Email,
                Name = u.Name,
                Role = u.Role.ToString(),
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt,
                PortfolioCount = portfolioIds.Count,
                TradeCount = tradeCount,
                LastTradeAt = lastTradeAt,
                LastImpersonatedAt = lastImpersonated,
            });
        }

        return new UsersOverviewResult
        {
            Items = items,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
