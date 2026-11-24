using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Journals.Queries.GetJournals;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Journals.Queries;

/// <summary>
/// Nhánh có PortfolioId chỉ được trả nhật ký thuộc tài khoản đang gọi (web, ApiKey, MCP dùng chung handler này).
/// </summary>
public class GetJournalsQueryHandlerTests
{
    private readonly Mock<ITradeJournalRepository> _journalRepo = new();
    private readonly GetJournalsQueryHandler _handler;

    public GetJournalsQueryHandlerTests()
    {
        _handler = new GetJournalsQueryHandler(_journalRepo.Object);
    }

    private static TradeJournal Journal(string userId, string portfolioId, string reason) =>
        new($"trade-{Guid.NewGuid():N}", userId, portfolioId, reason, "", "", "", 5);

    private void RepoReturnsForPortfolio(string portfolioId, params TradeJournal[] journals) =>
        _journalRepo.Setup(r => r.GetByPortfolioIdAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(journals);

    [Fact]
    public async Task Handle_PortfolioOfAnotherUser_ReturnsEmpty()
    {
        RepoReturnsForPortfolio("pf-other", Journal("other-user", "pf-other", "NHAT-KY-CUA-NGUOI-KHAC"));

        var result = await _handler.Handle(
            new GetJournalsQuery { UserId = "user-1", PortfolioId = "pf-other" }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OwnPortfolio_ReturnsItsJournals()
    {
        RepoReturnsForPortfolio("pf-1",
            Journal("user-1", "pf-1", "LY-DO-1"),
            Journal("user-1", "pf-1", "LY-DO-2"));

        var result = (await _handler.Handle(
            new GetJournalsQuery { UserId = "user-1", PortfolioId = "pf-1" }, CancellationToken.None)).ToList();

        result.Select(j => j.EntryReason).Should().BeEquivalentTo(new[] { "LY-DO-1", "LY-DO-2" });
    }

    [Fact]
    public async Task Handle_UnknownPortfolioId_ReturnsEmpty()
    {
        RepoReturnsForPortfolio("pf-khong-ton-tai");

        var result = await _handler.Handle(
            new GetJournalsQuery { UserId = "user-1", PortfolioId = "pf-khong-ton-tai" }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OwnPortfolioWithJournalOfAnotherUser_ReturnsOnlyOwnJournals()
    {
        // Danh mục của tài khoản đang gọi có lẫn một nhật ký thuộc tài khoản khác.
        RepoReturnsForPortfolio("pf-1",
            Journal("user-1", "pf-1", "NHAT-KY-HOP-LE"),
            Journal("other-user", "pf-1", "NHAT-KY-KHONG-THUOC"));

        var result = (await _handler.Handle(
            new GetJournalsQuery { UserId = "user-1", PortfolioId = "pf-1" }, CancellationToken.None)).ToList();

        result.Select(j => j.EntryReason).Should().Equal("NHAT-KY-HOP-LE");
    }

    [Fact]
    public async Task Handle_WithoutPortfolioId_ReturnsAllJournalsOfTheUser()
    {
        _journalRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Journal("user-1", "pf-1", "A"), Journal("user-1", "pf-2", "B") });

        var result = (await _handler.Handle(
            new GetJournalsQuery { UserId = "user-1" }, CancellationToken.None)).ToList();

        result.Select(j => j.EntryReason).Should().BeEquivalentTo(new[] { "A", "B" });
    }
}
