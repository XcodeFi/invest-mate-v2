using System.ComponentModel.DataAnnotations;

namespace InvestmentApp.Application.Common;

/// <summary>
/// Chu kỳ thanh toán, đo bằng số PHIÊN GIAO DỊCH kể từ ngày khớp lệnh.
/// Thiếu section <c>Settlement</c> trong config thì giữ T+2 — chuẩn HOSE hiện hành.
/// </summary>
public class SettlementOptions
{
    public const string SectionName = "Settlement";

    /// <summary>T+2. Giá trị áp dụng khi config không nói gì.</summary>
    public const int DefaultSessions = 2;

    /// <summary>
    /// Số phiên. <c>0</c> nghĩa là tiền về ngay trong ngày (T+0) nên không bao giờ có
    /// "chờ về" — đó là một giá trị HỢP LỆ, không phải "chưa cấu hình". Đừng viết
    /// <c>Sessions == 0 ? DefaultSessions : Sessions</c> ở bất kỳ đâu: làm vậy là bịt
    /// hẳn đường đặt T+0 mà không có test nào đỏ.
    /// </summary>
    [Range(0, 10)]
    public int Sessions { get; set; } = DefaultSessions;
}
