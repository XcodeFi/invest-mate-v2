using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Điều chỉnh một mức giá tuyệt đối (giá vào, cắt lỗ, mục tiêu) theo các sự kiện quyền
/// xảy ra SAU khi mức giá đó được đặt. Áp dụng tại thời điểm đọc — không sửa dữ liệu,
/// nên xoá sự kiện thì ngưỡng tự quay về giá trị cũ.
/// </summary>
public static class CorporateActionAdjuster
{
    public static decimal AdjustPrice(decimal price, DateTime setAt, IEnumerable<CorporateAction> actions)
    {
        var setAtDate = setAt.Date;
        var ordered = actions
            .Where(a => a.ExDate.Date > setAtDate)
            .OrderBy(a => a.ExDate.Date)
            .ThenBy(a => a.Type == CorporateActionType.CashDividend ? 0 : 1);

        var result = price;
        foreach (var a in ordered)
        {
            if (a.Type == CorporateActionType.CashDividend)
                result -= a.AmountPerShare ?? 0m;
            else
                result /= a.Multiplier;
        }
        return result;
    }
}
