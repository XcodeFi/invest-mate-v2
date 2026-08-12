using System.Globalization;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Api.Mcp;

/// <summary>
/// Câu cảnh báo trần biến động nối vào kết quả tạo/sửa kế hoạch qua MCP.
/// <para>
/// Trên form web panel tự hiện nên người dùng không tránh được. Qua agent thì <c>get_volatility_sizing</c>
/// là một tool RIÊNG, và lời dặn "gọi trước khi create_trade_plan" lại nằm trên chính cái tool đó —
/// agent đi thẳng vào tạo kế hoạch sẽ không thấy gì. Lời dặn không phải cơ chế; đây là cơ chế.
/// </para>
/// <para>
/// Vẫn KHÔNG chặn (ADR-0014, Đ4): kế hoạch được tạo bình thường, câu này chỉ đi kèm kết quả.
/// </para>
/// </summary>
public static class McpVolatilityNotice
{
    // Dấu phẩy thập phân — toàn bộ text hiển thị của dự án dùng quy ước Việt Nam.
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private static string Pct(decimal? value) =>
        value is null ? "n/a" : value.Value.ToString("0.#", Vi) + "%/năm";

    /// <returns>
    /// <c>null</c> khi không có gì để hành động — trong trần, hoặc không bị biến động ràng buộc.
    /// Im lặng ở hai ca đó là đúng: nối một dòng vào MỌI lời gọi biến cảnh báo thành tiếng ồn và
    /// agent sẽ học cách bỏ qua. Nhưng ca KHÔNG TÍNH ĐƯỢC thì phải nói, vì im lặng ở đó đọc thành
    /// "đã kiểm và ổn" — ngược hẳn sự thật.
    /// </returns>
    public static string? Describe(VolatilitySizingResult? result, int quantity)
    {
        if (result is null || result.DataQuality == VolatilityDataQuality.Insufficient)
            return "⚠️ Trần khối lượng theo ngân sách biến động: chưa kiểm được (thiếu lịch sử giá "
                 + "hoặc chưa gắn danh mục). Kế hoạch vẫn được tạo — hãy tự xem lại khối lượng.";

        if (result.PortfolioAlreadyOverBudget)
            return $"⚠️ Danh mục đã vượt ngân sách biến động trước khi thêm lệnh này "
                 + $"({Pct(result.CurrentVolatilityPercent)} so với ngân sách {Pct(result.BudgetVolatilityPercent)}). "
                 + "Mọi lệnh mua thêm đều làm xấu thêm — cần cơ cấu lại danh mục, không phải chỉnh khối lượng lệnh này.";

        if (result.IsUnconstrainedByVolatility || result.MaxQuantityWithinBudget is not int ceiling)
            return null;

        if (quantity <= ceiling) return null;

        return $"⚠️ Khối lượng {quantity:N0} vượt trần theo ngân sách biến động là {ceiling:N0} cổ. "
             + $"Biến động danh mục sẽ lên {Pct(result.ProjectedVolatilityPercent)}, vượt ngân sách "
             + $"{Pct(result.BudgetVolatilityPercent)}. Kế hoạch vẫn được tạo ở trạng thái Nháp — "
             + $"cân nhắc giảm còn {ceiling:N0} cổ trước khi chuyển sang Sẵn sàng.";
    }
}
