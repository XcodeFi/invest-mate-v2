using System.ComponentModel;
using InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.DTOs;
using InvestmentApp.Application.CompanyDossiers.Queries.GetCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Queries.GetDossierGateStatus;
using InvestmentApp.Application.CompanyDossiers.Queries.ListCompanyDossiers;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

/// <summary>
/// Tool hồ sơ công ty cho agent. CỐ Ý KHÔNG CÓ tool ký/xác nhận hồ sơ: `ConfirmedAt` chỉ đặt được
/// qua endpoint JWT `POST /company-dossiers/{symbol}/confirm`, tức chỉ con người đặt được
/// (ADR-0011 D2). Một cổng mà agent tự thoả mãn được thì không đo được hiểu biết của người bỏ tiền,
/// nó chỉ đo "agent đã điền gì đó". Thêm tool ký vào đây là phá bỏ toàn bộ lý do tính năng tồn tại.
/// </summary>
[McpServerToolType]
public static class CompanyDossierTools
{
    [McpServerTool(Name = "list_company_dossiers", ReadOnly = true)]
    [Description("Liệt kê mọi hồ sơ công ty của chủ khoá, kèm trạng thái hạn tươi (Unconfirmed/Fresh/NeedsReview/Expired).")]
    public static async Task<List<CompanyDossierDto>> ListCompanyDossiers(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new ListCompanyDossiersQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "get_company_dossier", ReadOnly = true)]
    [Description("Lấy hồ sơ công ty của một mã. Null nếu chưa có hồ sơ cho mã đó.")]
    public static async Task<CompanyDossierDto?> GetCompanyDossier(
        [Description("Mã chứng khoán.")] string symbol,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetCompanyDossierQuery { UserId = http.GetUserId(), Symbol = symbol }, ct);

    [McpServerTool(Name = "get_dossier_gate_status", ReadOnly = true)]
    [Description("Kiểm hồ sơ công ty có đủ cho một lệnh dự kiến hay chưa, TRƯỚC khi gọi create_trade_plan. "
        + "Trả reason (missing/unconfirmed/expired/insufficient) và missing[] — danh sách chính xác những gì còn thiếu. "
        + "Cả ba tham số đều bắt buộc: ngưỡng đủ nội dung phụ thuộc quy mô lệnh, nên thiếu một số là chấm sai bậc.")]
    public static async Task<DossierGateStatusDto> GetDossierGateStatus(
        [Description("Mã chứng khoán.")] string symbol,
        [Description("Khối lượng dự kiến mua.")] int quantity,
        [Description("Giá vào dự kiến, VND.")] decimal entryPrice,
        [Description("Giá trị tài khoản, VND — dùng để tính lệnh này chiếm bao nhiêu % tài khoản.")] decimal accountBalance,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetDossierGateStatusQuery
        {
            UserId = http.GetUserId(),
            Symbol = symbol,
            Quantity = quantity,
            EntryPrice = entryPrice,
            AccountBalance = accountBalance
        }, ct);

    [McpServerTool(Name = "upsert_company_dossier", Destructive = true)]
    [Description("Soạn hoặc cập nhật hồ sơ công ty cho một mã. Agent soạn được nội dung nhưng KHÔNG ký được — "
        + "sau khi gọi tool này, hồ sơ về trạng thái chờ người dùng đọc và ký trên trang /company-dossier/{mã}, "
        + "và cổng lập kế hoạch vẫn chặn cho tới khi có chữ ký. Ghi đè nội dung cũ, không cộng dồn. "
        + "Mỗi yếu tố rủi ro BẮT BUỘC có observableSignal (dấu hiệu quan sát được), và tối đa MỘT yếu tố "
        + "được đánh isDealBreaker — vi phạm sẽ bị từ chối.")]
    public static async Task<string> UpsertCompanyDossier(
        [Description("Mã chứng khoán.")] string symbol,
        [Description("Doanh nghiệp kiếm tiền bằng gì — mô tả cơ chế thật, không dùng 'tiềm năng'/'đầu ngành'. "
            + "Lệnh từ 5% tài khoản trở lên cần ≥ 30 ký tự.")] string businessModel,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Các lợi thế bền (moat). Bỏ trống = không ghi nhận; lệnh lớn cần ít nhất 1 moat mô tả ≥ 30 ký tự.")]
        List<MoatItem>? moats = null,
        [Description("Các yếu tố rủi ro xếp hạng 1..N (1 = nguy hiểm nhất). Mỗi yếu tố phải có observableSignal. "
            + "Lệnh lớn cần ≥ 3 yếu tố, mỗi dấu hiệu ≥ 20 ký tự.")]
        List<RiskFactor>? riskFactors = null,
        [Description("Ghi chú tự do — KHÔNG ảnh hưởng điều kiện chặn của cổng.")] string? notes = null)
        // Confirm/ConfirmedAt cố tình không mở ra MCP — chỉ con người ký (ADR-0011 D2).
        => await mediator.Send(new UpsertCompanyDossierCommand
        {
            UserId = http.GetUserId(),
            Symbol = symbol,
            BusinessModel = businessModel,
            Moats = moats ?? new(),
            RiskFactors = riskFactors ?? new(),
            Notes = notes,
            ByAgent = true   // cửa MCP luôn là agent — kéo theo phải ký lại (Q10)
        }, ct);
}
