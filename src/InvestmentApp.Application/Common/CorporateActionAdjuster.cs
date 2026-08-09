using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Điều chỉnh một mức giá tuyệt đối (giá vào, cắt lỗ, mục tiêu) theo các sự kiện quyền
/// xảy ra SAU khi mức giá đó được đặt. Áp dụng tại thời điểm đọc — không sửa dữ liệu,
/// nên xoá sự kiện thì ngưỡng tự quay về giá trị cũ.
/// </summary>
public static class CorporateActionAdjuster
{
    /// <summary>
    /// Chỉ áp các sự kiện có ngày GDKHQ nằm SAU <paramref name="setAt"/> và không muộn hơn
    /// <paramref name="asOf"/>. Chặn trên là bắt buộc: sự kiện thường được công bố trước
    /// ngày GDKHQ vài tuần, mà giá thị trường thì chưa điều chỉnh trong khoảng đó.
    /// </summary>
    public static decimal AdjustPrice(decimal price, DateTime setAt, IEnumerable<CorporateAction> actions,
        DateTime? asOf = null)
    {
        var setAtDate = setAt.Date;
        var asOfDate = (asOf ?? DateTime.UtcNow).Date;
        var ordered = actions
            .Where(a => a.ExDate.Date > setAtDate && a.ExDate.Date <= asOfDate)
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

    /// <summary>
    /// Điều chỉnh một KHOẢNG CÁCH giá (biên trượt, bước nhảy) thay vì một mức giá.
    /// Chỉ co theo hệ số chia; cổ tức tiền mặt dịch cả mặt bằng giá nên không
    /// làm khoảng cách hẹp lại.
    /// </summary>
    public static decimal AdjustDelta(decimal delta, DateTime setAt, IEnumerable<CorporateAction> actions,
        DateTime? asOf = null)
    {
        var setAtDate = setAt.Date;
        var asOfDate = (asOf ?? DateTime.UtcNow).Date;
        var result = delta;
        foreach (var a in actions.Where(a =>
                     a.ExDate.Date > setAtDate && a.ExDate.Date <= asOfDate
                     && a.Type != CorporateActionType.CashDividend))
        {
            result /= a.Multiplier;
        }
        return result;
    }
}
