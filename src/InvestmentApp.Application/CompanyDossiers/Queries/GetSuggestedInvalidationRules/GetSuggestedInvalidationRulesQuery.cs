using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CompanyDossiers.Queries.GetSuggestedInvalidationRules;

public class GetSuggestedInvalidationRulesQuery : IRequest<List<SuggestedInvalidationRuleDto>>
{
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
}

public class SuggestedInvalidationRuleDto
{
    public InvalidationTrigger Trigger { get; set; }
    public string Detail { get; set; } = null!;

    /// <summary>
    /// Detail có đạt ngưỡng 20 ký tự của gate kỷ luật hay chưa. Đề xuất không đạt vẫn được trả về:
    /// người dùng bổ sung được, còn lặng lẽ bỏ đi thì họ không biết là có gợi ý.
    /// </summary>
    public bool MeetsMinLength { get; set; }

    /// <summary>Hạng của rủi ro sinh ra đề xuất này (1 = nguy hiểm nhất).</summary>
    public int SourceRank { get; set; }
}

/// <summary>
/// Hồ sơ đã buộc trả lời "rủi ro nào + biết nó đang xảy ra bằng dấu hiệu gì" — đúng nguyên liệu của
/// `InvalidationRule` trên trade plan. Bắt gõ lại lần thứ hai là chỗ tính năng này lấy thời gian của
/// người dùng mà không trả lại gì. Chỉ ĐỀ XUẤT: người dùng tick mới vào plan.
/// </summary>
public class GetSuggestedInvalidationRulesQueryHandler
    : IRequestHandler<GetSuggestedInvalidationRulesQuery, List<SuggestedInvalidationRuleDto>>
{
    /// <summary>Ngưỡng Detail của gate kỷ luật thesis hiện có.</summary>
    private const int MinDetailLength = 20;

    private readonly ICompanyDossierRepository _repo;

    public GetSuggestedInvalidationRulesQueryHandler(ICompanyDossierRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<SuggestedInvalidationRuleDto>> Handle(
        GetSuggestedInvalidationRulesQuery request, CancellationToken cancellationToken)
    {
        var symbol = (request.Symbol ?? string.Empty).Trim().ToUpperInvariant();
        var dossier = await _repo.GetAsync(request.UserId, symbol);
        if (dossier == null) return new();

        return dossier.RiskFactors
            .OrderBy(r => r.Rank)
            .Take(3)
            .Select(r =>
            {
                var detail = $"{r.Description} — dấu hiệu: {r.ObservableSignal}";
                return new SuggestedInvalidationRuleDto
                {
                    // Rủi ro không chọn kịch bản vẫn phải đề xuất được — Manual là chỗ để nó đi tiếp.
                    Trigger = r.SuggestedTrigger ?? InvalidationTrigger.Manual,
                    Detail = detail,
                    MeetsMinLength = detail.Trim().Length >= MinDetailLength,
                    SourceRank = r.Rank
                };
            })
            .ToList();
    }
}
