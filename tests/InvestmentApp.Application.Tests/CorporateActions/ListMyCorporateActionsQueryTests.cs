using FluentAssertions;
using InvestmentApp.Application.CorporateActions.Queries.ListMyCorporateActions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;
using Xunit;

namespace InvestmentApp.Application.Tests.CorporateActions;

/// <summary>
/// list_corporate_actions (MCP): người gọi chỉ thấy sự kiện quyền của danh mục CỦA MÌNH. Danh mục không có và danh mục của
/// người khác cho cùng một phản hồi (không tìm thấy), để không dò được id nào tồn tại.
/// </summary>
public class ListMyCorporateActionsQueryTests
{
    private readonly Mock<ICorporateActionRepository> _actions = new();
    private readonly Mock<IPortfolioRepository> _portfolios = new();

    private static Portfolio Owned(string userId, string id)
    {
        var p = new Portfolio(userId, "DM " + id, 0);
        typeof(Portfolio).BaseType!.GetProperty("Id")!.SetValue(p, id);
        return p;
    }

    private static CorporateAction Dividend(string portfolioId, string userId, string symbol) =>
        CorporateAction.CashDividend(portfolioId, userId, symbol, 10m, new DateTime(2026, 6, 10), null, 5m);

    private ListMyCorporateActionsQueryHandler Sut() => new(_actions.Object, _portfolios.Object);

    private void Setup(params Portfolio[] mine)
        => _portfolios.Setup(r => r.GetByUserIdAsync("u1", It.IsAny<CancellationToken>())).ReturnsAsync(mine);

    [Fact]
    public async Task without_a_portfolio_returns_actions_across_all_the_callers_portfolios_and_only_those()
    {
        Setup(Owned("u1", "p1"), Owned("u1", "p2"));
        IEnumerable<string>? asked = null;
        _actions.Setup(r => r.GetByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, CancellationToken>((ids, _) => asked = ids.ToList())
            .ReturnsAsync(new[] { Dividend("p1", "u1", "HPG"), Dividend("p2", "u1", "FPT") });

        var result = await Sut().Handle(new ListMyCorporateActionsQuery("u1", null, null), CancellationToken.None);

        asked.Should().BeEquivalentTo(new[] { "p1", "p2" });
        result.Select(r => r.Symbol).Should().BeEquivalentTo(new[] { "HPG", "FPT" });
        result.Select(r => r.PortfolioId).Should().BeEquivalentTo(new[] { "p1", "p2" });
    }

    [Fact]
    public async Task a_portfolio_filter_narrows_to_that_portfolio()
    {
        Setup(Owned("u1", "p1"), Owned("u1", "p2"));
        IEnumerable<string>? asked = null;
        _actions.Setup(r => r.GetByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, CancellationToken>((ids, _) => asked = ids.ToList())
            .ReturnsAsync(new[] { Dividend("p2", "u1", "FPT") });

        var result = await Sut().Handle(new ListMyCorporateActionsQuery("u1", "p2", null), CancellationToken.None);

        asked.Should().BeEquivalentTo(new[] { "p2" });
        result.Should().ContainSingle().Which.PortfolioId.Should().Be("p2");
    }

    [Fact]
    public async Task a_symbol_filter_is_case_insensitive_and_trimmed()
    {
        Setup(Owned("u1", "p1"));
        _actions.Setup(r => r.GetByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Dividend("p1", "u1", "HPG"), Dividend("p1", "u1", "FPT") });

        var result = await Sut().Handle(new ListMyCorporateActionsQuery("u1", null, " hpg "), CancellationToken.None);

        result.Should().ContainSingle().Which.Symbol.Should().Be("HPG");
    }

    [Fact]
    public async Task a_foreign_portfolio_id_and_a_missing_one_give_the_same_not_found_and_read_nothing()
    {
        // p-foreign thuộc người khác nên không nằm trong danh sách danh mục của u1; p-missing thì không tồn tại
        Setup(Owned("u1", "p1"));

        var foreign = await FluentActions.Awaiting(() =>
            Sut().Handle(new ListMyCorporateActionsQuery("u1", "p-foreign", null), CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();
        var missing = await FluentActions.Awaiting(() =>
            Sut().Handle(new ListMyCorporateActionsQuery("u1", "p-missing", null), CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();

        foreign.Which.Message.Replace("p-foreign", "X").Should().Be(missing.Which.Message.Replace("p-missing", "X"));
        _actions.Verify(r => r.GetByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task a_caller_without_portfolios_gets_an_empty_list()
    {
        Setup();
        _actions.Setup(r => r.GetByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CorporateAction>());

        var result = await Sut().Handle(new ListMyCorporateActionsQuery("u1", null, null), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
