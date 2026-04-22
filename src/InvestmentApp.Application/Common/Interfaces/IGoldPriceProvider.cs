using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common.Interfaces;

/// <summary>
/// Fetches current gold prices (Miếng + Nhẫn across SJC/DOJI/PNJ/Other brands).
/// Implemented in Phase 3 by HmoneyGoldPriceProvider (24hmoney crawler).
/// </summary>
public interface IGoldPriceProvider
{
    /// <summary>Lấy toàn bộ bảng giá vàng hiện tại. Only Miếng + Nhẫn (không trả nữ trang/trang sức).</summary>
    Task<IReadOnlyList<GoldPriceDto>> GetPricesAsync(CancellationToken cancellationToken = default);

    /// <summary>Lấy giá 1 combo brand+type. Trả null nếu không có data (crawler fail hoặc combo không tồn tại).</summary>
    Task<GoldPriceDto?> GetPriceAsync(GoldBrand brand, GoldType type, CancellationToken cancellationToken = default);
}
