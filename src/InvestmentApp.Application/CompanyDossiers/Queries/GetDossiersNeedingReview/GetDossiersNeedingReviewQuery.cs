using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CompanyDossiers.Queries.GetDossiersNeedingReview;

public class GetDossiersNeedingReviewQuery : IRequest<List<DossierReviewItemDto>>
{
    public string UserId { get; set; } = null!;
}

public class DossierReviewItemDto
{
    public string Symbol { get; set; } = null!;
    public string Freshness { get; set; } = null!;
    public DateTime ReviewedAt { get; set; }

    /// <summary>
    /// Số ngày quá mốc 90 ngày (theo ngày lịch VN). Hồ sơ chưa ký thì bằng 0 — đồng hồ hạn tươi
    /// chưa chạy, hiện một con số quá hạn ở đó là bịa.
    /// </summary>
    public int DaysOverdue { get; set; }
}

/// <summary>
/// Cổng hồ sơ chỉ bắn lúc lập kế hoạch, nên một hồ sơ hết hạn chỉ lộ ra đúng lúc người dùng đang
/// muốn mua — lúc tệ nhất để phải ngồi đọc lại. Danh sách này là đường duy nhất để biết trước, nên
/// thứ tự phải đưa cái sắp chặn mình lên trước.
/// </summary>
public class GetDossiersNeedingReviewQueryHandler
    : IRequestHandler<GetDossiersNeedingReviewQuery, List<DossierReviewItemDto>>
{
    private readonly ICompanyDossierRepository _repo;

    public GetDossiersNeedingReviewQueryHandler(ICompanyDossierRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<DossierReviewItemDto>> Handle(
        GetDossiersNeedingReviewQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dossiers = await _repo.GetByUserIdAsync(request.UserId);

        return dossiers
            .Select(d => new { Dossier = d, Freshness = d.GetFreshness(now) })
            .Where(x => x.Freshness != DossierFreshness.Fresh)
            .Select(x => new DossierReviewItemDto
            {
                Symbol = x.Dossier.Symbol,
                Freshness = x.Freshness.ToString(),
                ReviewedAt = x.Dossier.ReviewedAt,
                // Ngưỡng 90 ngày + phép lệch giờ VN sống ở entity, không nhân bản ở đây.
                DaysOverdue = x.Dossier.DaysOverdueForReview(now)
            })
            .OrderBy(x => Severity(x.Freshness))
            .ThenByDescending(x => x.DaysOverdue)
            .ToList();
    }

    /// <summary>
    /// Expired (đang chặn) trước, rồi Unconfirmed (cổng cũng coi như không có hồ sơ), rồi NeedsReview
    /// (chỉ nhắc). Thứ tự này là "cái nào chặn mình trước thì lên trước", không phải thứ tự chữ cái.
    /// </summary>
    private static int Severity(string freshness) => freshness switch
    {
        nameof(DossierFreshness.Expired) => 0,
        nameof(DossierFreshness.Unconfirmed) => 1,
        _ => 2
    };
}
