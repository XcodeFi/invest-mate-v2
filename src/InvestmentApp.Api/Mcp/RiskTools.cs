using System.ComponentModel;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetCorrelation;
using InvestmentApp.Application.Risk.Queries.GetDrawdown;
using InvestmentApp.Application.Risk.Queries.GetPortfolioOptimization;
using InvestmentApp.Application.Risk.Queries.GetPortfolioRisk;
using InvestmentApp.Application.Risk.Queries.GetRiskBudget;
using InvestmentApp.Application.Risk.Queries.GetRiskProfile;
using InvestmentApp.Application.Risk.Queries.GetSectorExposureForPlan;
using InvestmentApp.Application.Risk.Queries.GetStopLossTargets;
using InvestmentApp.Application.Risk.Queries.GetTrailingStopAlerts;
using InvestmentApp.Application.Risk.Queries.GetVolatilitySizingForPlan;
using InvestmentApp.Application.TradePlans.Queries.GetEstimateCalibration;
using InvestmentApp.Application.TradePlans.Queries.GetScenarioAdvisories;
using InvestmentApp.Application.TradePlans.Queries.GetStrategyExpectancy;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

[McpServerToolType]
public static class RiskTools
{
    [McpServerTool(Name = "get_portfolio_risk", ReadOnly = true)]
    [Description("Tổng quan rủi ro danh mục: volatility, VaR, Sharpe, max drawdown + chi tiết từng vị thế (khoảng cách tới SL).")]
    public static async Task<PortfolioRiskSummary> GetPortfolioRisk(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetPortfolioRiskQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_stop_loss_targets", ReadOnly = true)]
    [Description("Các mức stop-loss / target đang đặt cho một danh mục, kèm trạng thái triggered và risk-reward ratio.")]
    public static async Task<StopLossTargetsDto> GetStopLossTargets(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetStopLossTargetsQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_trailing_stop_alerts", ReadOnly = true)]
    [Description("Cảnh báo vị thế sát ngưỡng trailing stop trong một danh mục (severity: danger ≤2%, warning ≤5%).")]
    public static async Task<TrailingStopAlertsResult> GetTrailingStopAlerts(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetTrailingStopAlertsQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_volatility_sizing", ReadOnly = true)]
    [Description("Trần khối lượng theo ngân sách biến động cho một lệnh MUA dự kiến: biến động danh mục trước/sau, tương quan với danh mục, đóng góp rủi ro biên, và số cổ tối đa còn nằm trong ngân sách. Gọi TRƯỚC khi create_trade_plan để không đặt khối lượng vượt trần.")]
    public static async Task<VolatilitySizingResult> GetVolatilitySizing(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        [Description("Mã chứng khoán dự kiến mua, ví dụ FPT.")] string symbol,
        [Description("Giá vào lệnh dự kiến, đơn vị đồng.")] decimal entryPrice,
        [Description("Số cổ dự kiến mua.")] int quantity,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetVolatilitySizingForPlanQuery
        {
            UserId = http.GetUserId(),
            PortfolioId = portfolioId,
            Symbol = symbol,
            EntryPrice = entryPrice,
            Quantity = quantity
        }, ct);

    [McpServerTool(Name = "get_scenario_advisories", ReadOnly = true)]
    [Description("Cảnh báo kịch bản (scenario) đang vi phạm điều kiện trên các trade plan active.")]
    public static async Task<List<ScenarioAdvisory>> GetScenarioAdvisories(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetScenarioAdvisoriesQuery { UserId = http.GetUserId() }, ct);

    // ---------- Đợt 2: rủi ro mở rộng. Mọi tool lấy UserId CHỈ từ người gọi (http.GetUserId()), không có tham số userId;
    // handler tương ứng kiểm danh mục thuộc người gọi trước khi đọc dữ liệu (RiskQueriesOwnershipTests). ----------

    [McpServerTool(Name = "get_risk_profile", ReadOnly = true)]
    [Description("Hồ sơ rủi ro đã thiết lập cho một danh mục: tỷ trọng vị thế/ngành tối đa, ngưỡng cảnh báo drawdown, R:R mặc định, rủi ro danh mục tối đa, số lệnh/ngày và ngưỡng lỗ/ngày. Trả về rỗng (null) nếu chưa thiết lập.")]
    public static async Task<RiskProfileDto?> GetRiskProfile(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetRiskProfileQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_drawdown", ReadOnly = true)]
    [Description("Phân tích drawdown của danh mục: mức sụt giảm tối đa và hiện tại (%), đỉnh và đáy (ngày, giá trị) cùng chuỗi drawdown theo ngày.")]
    public static async Task<DrawdownResult> GetDrawdown(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetDrawdownQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_correlation", ReadOnly = true)]
    [Description("Tương quan giá 90 ngày giữa từng cặp mã trong danh mục (danh sách cặp kèm hệ số). Danh mục dưới 2 mã thì không có cặp nào.")]
    public static async Task<CorrelationMatrix> GetCorrelation(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetCorrelationQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_portfolio_optimization", ReadOnly = true)]
    [Description("Phân tích tối ưu danh mục: điểm đa dạng hoá, cảnh báo tập trung vào một mã, tỷ trọng theo ngành, cảnh báo cặp mã tương quan cao và danh sách khuyến nghị. Chỉ là tham khảo, không tạo lệnh.")]
    public static async Task<PortfolioOptimizationResult> GetPortfolioOptimization(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetPortfolioOptimizationQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_sector_exposure", ReadOnly = true)]
    [Description("Tỷ trọng ngành hiện tại của danh mục và SAU một lệnh mua dự kiến (mã + giá trị lệnh). Gọi trước khi lập kế hoạch để biết lệnh có đẩy một ngành vượt ngưỡng không.")]
    public static async Task<SectorExposureForPlan> GetSectorExposure(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        [Description("Mã chứng khoán dự kiến mua, ví dụ HPG.")] string symbol,
        [Description("Giá trị lệnh dự kiến, đơn vị đồng (giá × số lượng).")] decimal addValue,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Cần nhập mã chứng khoán (symbol), ví dụ HPG.", nameof(symbol));
        return await mediator.Send(new GetSectorExposureForPlanQuery
        {
            UserId = http.GetUserId(),
            PortfolioId = portfolioId,
            Symbol = symbol.ToUpper().Trim(),
            AddValue = addValue
        }, ct);
    }

    [McpServerTool(Name = "get_risk_budget", ReadOnly = true)]
    [Description("Ngân sách rủi ro trong ngày của danh mục: số lệnh đã đặt và mức lỗ trong ngày so với giới hạn trong hồ sơ rủi ro.")]
    public static async Task<RiskBudgetStatus> GetRiskBudget(
        [Description("ID danh mục (lấy từ list_portfolios).")] string portfolioId,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetRiskBudgetQuery { UserId = http.GetUserId(), PortfolioId = portfolioId }, ct);

    [McpServerTool(Name = "get_strategy_expectancy", ReadOnly = true)]
    [Description("Kỳ vọng theo R của các chiến dịch ĐÃ ĐÓNG (kế hoạch Mua đã review), nhóm theo chiến lược: số mẫu, % thắng kèm khoảng tin cậy 95%, R thắng/thua trung bình, kỳ vọng R (gồm cả lệnh hoà) và profit factor theo R. Dưới 10 mẫu thì chưa đủ tin. Giá trị null nghĩa là chưa đủ dữ liệu, không phải 0.")]
    public static async Task<StrategyExpectancyDto> GetStrategyExpectancy(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
        => await mediator.Send(new GetStrategyExpectancyQuery { UserId = http.GetUserId() }, ct);

    [McpServerTool(Name = "get_estimate_calibration", ReadOnly = true)]
    [Description("Độ khớp giữa % thắng người dùng đã ước lượng khi vào lệnh và kết quả thật của các chiến dịch đã review (chỉ kế hoạch Mua có ước lượng đã khoá, hoà không tính): 4 mức ước lượng (0–40, 40–55, 55–70, 70–100%) với n, ước lượng trung bình và tỉ lệ thắng thật; mức n dưới 5 là chưa đủ mẫu. brier và optimismPoints (dương = lạc quan, điểm %) chỉ có khi tổng n từ 15 trở lên, dưới ngưỡng thì vắng mặt (không phải 0); trường sufficient cho biết đủ ngưỡng hay chưa. Các tỉ lệ là phân số 0–1.")]
    public static async Task<InvestmentApp.Domain.Services.CalibrationResult> GetEstimateCalibration(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        [Description("Chỉ tính một chiến lược (bỏ trống = tất cả). ID lạ cho kết quả rỗng.")] string? strategyId = null)
        => await mediator.Send(new GetEstimateCalibrationQuery { UserId = http.GetUserId(), StrategyId = strategyId }, ct);
}
