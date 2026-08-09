using FluentAssertions;
using InvestmentApp.Application.CorporateActions.Commands.CreateCorporateAction;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.CorporateActions;

public class CreateCorporateActionCommandHandlerTests
{
    private readonly Mock<ICorporateActionRepository> _actions = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();
    private readonly CreateCorporateActionCommandHandler _handler;

    public CreateCorporateActionCommandHandlerTests()
        => _handler = new CreateCorporateActionCommandHandler(_actions.Object, _portfolios.Object);

    private static CreateCorporateActionCommand CashCommand() => new(
        UserId: "u1", PortfolioId: "p1", Symbol: "sab",
        Type: CorporateActionType.CashDividend,
        ExDate: new DateTime(2026, 6, 10), SettlementDate: new DateTime(2026, 7, 10),
        PercentOfPar: 5m, TaxRatePercent: 5m, RatioOld: null, RatioNew: null, Note: null);

    private void PortfolioOwnedBy(string userId) =>
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Portfolio(userId, "Danh mục", 0));

    [Fact]
    public async Task DanhMucCuaNguoiKhac_ThiNemUnauthorized()
    {
        PortfolioOwnedBy("u2");

        var act = () => _handler.Handle(CashCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _actions.Verify(r => r.AddAsync(It.IsAny<CorporateAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DanhMucKhongTonTai_ThiNemArgumentException()
    {
        _portfolios.Setup(r => r.GetByIdAsync("p1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        var act = () => _handler.Handle(CashCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CoTucTienMat_LuuVoiSoTienDaQuyDoi()
    {
        PortfolioOwnedBy("u1");
        CorporateAction? saved = null;
        _actions.Setup(r => r.AddAsync(It.IsAny<CorporateAction>(), It.IsAny<CancellationToken>()))
            .Callback<CorporateAction, CancellationToken>((a, _) => saved = a)
            .Returns(Task.CompletedTask);

        var id = await _handler.Handle(CashCommand(), CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Symbol.Should().Be("SAB");
        saved.AmountPerShare.Should().Be(500m);
        saved.PortfolioId.Should().Be("p1");
        id.Should().Be(saved.Id);
    }

    [Fact]
    public async Task CoTucCoPhieu_LuuVoiTyLe()
    {
        PortfolioOwnedBy("u1");
        CorporateAction? saved = null;
        _actions.Setup(r => r.AddAsync(It.IsAny<CorporateAction>(), It.IsAny<CancellationToken>()))
            .Callback<CorporateAction, CancellationToken>((a, _) => saved = a)
            .Returns(Task.CompletedTask);

        var command = new CreateCorporateActionCommand("u1", "p1", "HPG",
            CorporateActionType.StockDividend, new DateTime(2026, 6, 10), null,
            null, null, 100m, 130m, null);

        await _handler.Handle(command, CancellationToken.None);

        saved!.Multiplier.Should().Be(1.3m);
    }

    [Fact]
    public async Task CoTucTienMatThieuTyLe_ThiNemArgumentException()
    {
        PortfolioOwnedBy("u1");

        var command = CashCommand() with { PercentOfPar = null };
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
