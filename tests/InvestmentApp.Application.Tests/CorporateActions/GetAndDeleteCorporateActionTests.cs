using FluentAssertions;
using InvestmentApp.Application.CorporateActions.Commands.DeleteCorporateAction;
using InvestmentApp.Application.CorporateActions.Queries.GetCorporateActions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.CorporateActions;

public class GetAndDeleteCorporateActionTests
{
    private readonly Mock<ICorporateActionRepository> _actions = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();

    private static CorporateAction Hpg() =>
        CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130,
            new DateTime(2026, 6, 10), new DateTime(2026, 7, 20));

    [Fact]
    public async Task Query_TraVeDanhSachCuaDanhMuc()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u1", "Danh mục", 0));
        _actions.Setup(r => r.GetByPortfolioIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Hpg() });

        var handler = new GetCorporateActionsQueryHandler(_actions.Object, _portfolios.Object);
        var result = await handler.Handle(new GetCorporateActionsQuery("u1", "p1", null), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("HPG");
        result[0].Multiplier.Should().Be(1.3m);
        result[0].DeclaredText.Should().Be("100:130");
    }

    [Fact]
    public async Task Query_DanhMucCuaNguoiKhac_ThiNemUnauthorized()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u2", "Danh mục", 0));

        var handler = new GetCorporateActionsQueryHandler(_actions.Object, _portfolios.Object);
        var act = () => handler.Handle(new GetCorporateActionsQuery("u1", "p1", null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Query_LocTheoMa_ThiGoiDungRepository()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio("u1", "Danh mục", 0));
        _actions.Setup(r => r.GetByPortfolioIdAndSymbolAsync("p1", "HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Hpg() });

        var handler = new GetCorporateActionsQueryHandler(_actions.Object, _portfolios.Object);
        var result = await handler.Handle(new GetCorporateActionsQuery("u1", "p1", "HPG"), CancellationToken.None);

        result.Should().HaveCount(1);
        _actions.Verify(r => r.GetByPortfolioIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_SuKienCuaNguoiKhac_ThiNemUnauthorized_VaKhongXoa()
    {
        var action = CorporateAction.StockDividend("p1", "u2", "HPG", 100, 130,
            new DateTime(2026, 6, 10), null);
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        var handler = new DeleteCorporateActionCommandHandler(_actions.Object);
        var act = () => handler.Handle(new DeleteCorporateActionCommand("u1", "a1"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _actions.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_SuKienDaSinhDongTien_ThiChanLai_TranhDongTienMoCoi()
    {
        var action = CorporateAction.CashDividend("p1", "u1", "SAB", 5m,
            new DateTime(2026, 6, 10), new DateTime(2026, 7, 10), 5m);
        action.LinkCapitalFlow("f1");
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(action);

        var handler = new DeleteCorporateActionCommandHandler(_actions.Object);
        var act = () => handler.Handle(new DeleteCorporateActionCommand("u1", "a1"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _actions.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_SuKienCuaMinh_ThiXoa()
    {
        _actions.Setup(r => r.GetByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(Hpg());

        var handler = new DeleteCorporateActionCommandHandler(_actions.Object);
        await handler.Handle(new DeleteCorporateActionCommand("u1", "a1"), CancellationToken.None);

        _actions.Verify(r => r.DeleteAsync("a1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
