using System.ComponentModel;
using InvestmentApp.Application.CorporateActions.Queries.ListMyCorporateActions;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class CorporateActionTools
{
    [McpServerTool(Name = "list_corporate_actions", ReadOnly = true)]
    [Description("Sự kiện quyền (cổ tức tiền mặt, cổ phiếu thưởng, chia tách) đã khai trên các danh mục của người dùng: mã, ngày giao dịch không hưởng quyền, ngày thanh toán, số tiền mỗi cổ (cổ tức ròng sau thuế), hệ số nhân. Bỏ trống portfolioId = mọi danh mục của người dùng. Danh mục không có hoặc của người khác cho cùng một phản hồi không tìm thấy.")]
    public static async Task<List<MyCorporateActionDto>> ListCorporateActions(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Chỉ một mã, ví dụ HPG (bỏ trống = tất cả).")] string? symbol = null,
        [Description("Chỉ một danh mục (lấy từ list_portfolios; bỏ trống = tất cả).")] string? portfolioId = null)
        => await mediator.Send(new ListMyCorporateActionsQuery(http.GetUserId(), portfolioId, symbol), ct);
}
