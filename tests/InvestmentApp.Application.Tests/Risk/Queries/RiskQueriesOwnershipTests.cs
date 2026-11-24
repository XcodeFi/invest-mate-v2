using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Risk.Queries.GetCorrelation;
using InvestmentApp.Application.Risk.Queries.GetDrawdown;
using InvestmentApp.Application.Risk.Queries.GetRiskBudget;
using InvestmentApp.Application.Risk.Queries.GetRiskProfile;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Risk.Queries;

/// <summary>
/// Kiểm chủ sở hữu của các query Risk trước khi bọc thành MCP (MCP là đường vào thứ hai, không được mở lại lỗ):
/// danh mục của người khác hoặc không tồn tại → ném lỗi cùng một thông điệp (không lộ danh mục có tồn tại hay không) và dịch vụ/
/// repository chứa dữ liệu danh mục KHÔNG bao giờ được gọi.
/// </summary>
public class RiskQueriesOwnershipTests
{
    private const string Me = "user-1";
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly Mock<IRiskCalculationService> _risk = new();
    private readonly Mock<IRiskProfileRepository> _profiles = new();

    private void Foreign() => _portfolios.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Portfolio("chu-so-huu-khac", "Danh mục người khác", 100_000_000m));

    private void Missing() => _portfolios.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
        .ReturnsAsync((Portfolio?)null);

    private void Mine() => _portfolios.Setup(r => r.GetByIdAsync("port1", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Portfolio(Me, "Danh mục của tôi", 100_000_000m));

    private void NoRiskServiceCalls() => _risk.VerifyNoOtherCalls();

    // ---------- get_risk_profile ----------

    [Fact]
    public async Task GetRiskProfile_foreign_portfolio_is_refused_and_the_profile_is_never_read()
    {
        Foreign();

        var act = () => new GetRiskProfileQueryHandler(_profiles.Object, _portfolios.Object)
            .Handle(new GetRiskProfileQuery { PortfolioId = "port1", UserId = Me }, default);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("access denied");
        _profiles.Verify(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetRiskProfile_missing_portfolio_gives_the_same_refusal()
    {
        Missing();

        var act = () => new GetRiskProfileQueryHandler(_profiles.Object, _portfolios.Object)
            .Handle(new GetRiskProfileQuery { PortfolioId = "port1", UserId = Me }, default);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("access denied");
    }

    [Fact]
    public async Task GetRiskProfile_own_portfolio_without_a_profile_returns_null_not_an_error()
    {
        Mine();
        _profiles.Setup(r => r.GetByPortfolioIdAsync("port1", It.IsAny<CancellationToken>())).ReturnsAsync((RiskProfile?)null);

        var result = await new GetRiskProfileQueryHandler(_profiles.Object, _portfolios.Object)
            .Handle(new GetRiskProfileQuery { PortfolioId = "port1", UserId = Me }, default);

        result.Should().BeNull();
    }

    // ---------- get_drawdown ----------

    [Fact]
    public async Task GetDrawdown_foreign_portfolio_is_refused_and_the_service_is_never_called()
    {
        Foreign();

        var act = () => new GetDrawdownQueryHandler(_risk.Object, _portfolios.Object)
            .Handle(new GetDrawdownQuery { PortfolioId = "port1", UserId = Me }, default);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("access denied");
        NoRiskServiceCalls();
    }

    [Fact]
    public async Task GetDrawdown_missing_portfolio_gives_the_same_refusal()
    {
        Missing();

        var act = () => new GetDrawdownQueryHandler(_risk.Object, _portfolios.Object)
            .Handle(new GetDrawdownQuery { PortfolioId = "port1", UserId = Me }, default);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("access denied");
        NoRiskServiceCalls();
    }

    // ---------- get_correlation ----------

    [Fact]
    public async Task GetCorrelation_foreign_portfolio_is_refused_and_the_service_is_never_called()
    {
        Foreign();

        var act = () => new GetCorrelationQueryHandler(_risk.Object, _portfolios.Object)
            .Handle(new GetCorrelationQuery { PortfolioId = "port1", UserId = Me }, default);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("access denied");
        NoRiskServiceCalls();
    }

    [Fact]
    public async Task GetCorrelation_missing_portfolio_gives_the_same_refusal()
    {
        Missing();

        var act = () => new GetCorrelationQueryHandler(_risk.Object, _portfolios.Object)
            .Handle(new GetCorrelationQuery { PortfolioId = "port1", UserId = Me }, default);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("access denied");
        NoRiskServiceCalls();
    }

    // ---------- get_risk_budget ----------

    [Fact]
    public async Task GetRiskBudget_foreign_portfolio_is_refused_and_the_service_is_never_called()
    {
        Foreign();

        var act = () => new GetRiskBudgetQueryHandler(_risk.Object, _portfolios.Object)
            .Handle(new GetRiskBudgetQuery { PortfolioId = "port1", UserId = Me }, default);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("access denied");
        NoRiskServiceCalls();
    }

    [Fact]
    public async Task GetRiskBudget_missing_portfolio_gives_the_same_refusal()
    {
        Missing();

        var act = () => new GetRiskBudgetQueryHandler(_risk.Object, _portfolios.Object)
            .Handle(new GetRiskBudgetQuery { PortfolioId = "port1", UserId = Me }, default);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().Contain("access denied");
        NoRiskServiceCalls();
    }

    // ---------- đường đi đúng vẫn chạy ----------

    [Fact]
    public async Task Own_portfolio_reaches_the_service_for_each_query()
    {
        Mine();
        _risk.Setup(r => r.CalculateMaxDrawdownAsync("port1", It.IsAny<CancellationToken>())).ReturnsAsync(new DrawdownResult());
        _risk.Setup(r => r.CalculateCorrelationMatrixAsync("port1", It.IsAny<CancellationToken>())).ReturnsAsync(new CorrelationMatrix());
        _risk.Setup(r => r.CheckRiskBudgetAsync("port1", It.IsAny<CancellationToken>())).ReturnsAsync(new RiskBudgetStatus());

        await new GetDrawdownQueryHandler(_risk.Object, _portfolios.Object)
            .Handle(new GetDrawdownQuery { PortfolioId = "port1", UserId = Me }, default);
        await new GetCorrelationQueryHandler(_risk.Object, _portfolios.Object)
            .Handle(new GetCorrelationQuery { PortfolioId = "port1", UserId = Me }, default);
        await new GetRiskBudgetQueryHandler(_risk.Object, _portfolios.Object)
            .Handle(new GetRiskBudgetQuery { PortfolioId = "port1", UserId = Me }, default);

        _risk.Verify(r => r.CalculateMaxDrawdownAsync("port1", It.IsAny<CancellationToken>()), Times.Once);
        _risk.Verify(r => r.CalculateCorrelationMatrixAsync("port1", It.IsAny<CancellationToken>()), Times.Once);
        _risk.Verify(r => r.CheckRiskBudgetAsync("port1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
