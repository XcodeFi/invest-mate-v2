using InvestmentApp.Application.CompanyDossiers.Gate;
using ModelContextProtocol;

namespace InvestmentApp.Api.Mcp;

/// <summary>
/// Dịch <see cref="DossierGateException"/> sang <see cref="McpException"/>. Qua MCP, exception
/// thường bị che thành "An error occurred invoking '<tên tool>'." — agent mất cả reason lẫn
/// missing[] nên không có đường tự chữa, dù chính nó vừa gây ra việc bị chặn.
/// </summary>
internal static class McpDossierGate
{
    private const string ReCheck =
        "Gọi get_dossier_gate_status(symbol, quantity, entryPrice, accountBalance) để xem chi tiết trước khi thử lại.";

    internal static async Task<T> GuardAsync<T>(Func<Task<T>> send)
    {
        try
        {
            return await send();
        }
        catch (DossierGateException ex)
        {
            throw new McpException(Describe(ex));
        }
    }

    private static string Describe(DossierGateException ex)
    {
        var symbol = ex.Symbol;
        var page = $"/company-dossier/{symbol}";

        var next = ex.Result.Reason switch
        {
            "missing" =>
                $"Chưa có hồ sơ công ty nào cho mã này. Gọi upsert_company_dossier để soạn nội dung, "
                + $"sau đó người dùng ký trên trang {page} — agent KHÔNG ký được.",

            "unconfirmed" =>
                $"Hồ sơ đã có nội dung nhưng chưa được người dùng ký. Agent KHÔNG ký được: "
                + $"người dùng phải ký trên trang {page}.",

            "expired" =>
                $"Chữ ký đã quá hạn soát lại. Agent KHÔNG ký được: người dùng phải xác nhận lại "
                + $"trên trang {page}. Dùng upsert_company_dossier nếu nội dung cần cập nhật trước khi ký.",

            "insufficient" =>
                $"Hồ sơ còn thiếu: {string.Join("; ", ex.Result.Missing)}. Gọi upsert_company_dossier "
                + $"để bổ sung, sau đó người dùng ký lại trên trang {page}.",

            _ => $"Gọi upsert_company_dossier để bổ sung nội dung, sau đó người dùng ký trên trang {page}.",
        };

        return $"Cổng hồ sơ công ty chặn mã {symbol} (reason={ex.Result.Reason}). {next} {ReCheck}";
    }
}
