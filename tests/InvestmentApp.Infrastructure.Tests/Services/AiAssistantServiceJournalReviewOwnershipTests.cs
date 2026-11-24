using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using MediatR;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Use case "journal-review" chỉ đưa nhật ký thuộc tài khoản đang gọi vào context gửi mô hình,
/// kể cả khi portfolioId trỏ tới danh mục khác.
/// </summary>
public class AiAssistantServiceJournalReviewOwnershipTests
{
    private const string Caller = "user-1";

    private static AiAssistantService BuildService(Mock<ITradeJournalRepository> journalRepo) =>
        new(
            new Mock<IAiSettingsRepository>().Object,
            new Mock<IAiKeyEncryptionService>().Object,
            new Mock<IAiChatServiceFactory>().Object,
            journalRepo.Object,
            new Mock<ITradeRepository>().Object,
            new Mock<IPortfolioRepository>().Object,
            new Mock<IPnLService>().Object,
            new Mock<ITradePlanRepository>().Object,
            new Mock<IFundamentalDataProvider>().Object,
            new Mock<IStockInfoProvider>().Object,
            new Mock<ITechnicalIndicatorService>().Object,
            new Mock<IRiskCalculationService>().Object,
            new Mock<IRiskProfileRepository>().Object,
            new Mock<IWatchlistRepository>().Object,
            new Mock<IComprehensiveStockDataProvider>().Object,
            new Mock<IFinancialProfileRepository>().Object,
            new PositionSizingService(),
            new Mock<IMarketDataProvider>().Object,
            new Mock<ICapitalFlowRepository>().Object,
            new Mock<IMarketClosureRepository>().Object,
            Microsoft.Extensions.Options.Options.Create(
                new InvestmentApp.Application.Common.SettlementOptions()),
            new Mock<IMediator>().Object);

    private static TradeJournal Journal(string userId, string portfolioId, string reason) =>
        new($"trade-{Guid.NewGuid():N}", userId, portfolioId, reason, "", "", "Bình tĩnh", 5);

    private static Task<AiContextResult> JournalReview(AiAssistantService svc, string portfolioId) =>
        svc.BuildContextAsync(
            useCase: "journal-review", userId: Caller, portfolioId: portfolioId,
            tradePlanId: null, symbol: null, question: null, year: null, month: null,
            message: null, history: null);

    [Fact]
    public async Task JournalReview_PortfolioOfAnotherUser_ExcludesTheirJournals()
    {
        var repo = new Mock<ITradeJournalRepository>();
        repo.Setup(r => r.GetByPortfolioIdAsync("pf-other", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Journal("other-user", "pf-other", "NHAT-KY-CUA-NGUOI-KHAC") });

        var result = await JournalReview(BuildService(repo), "pf-other");

        (result.UserMessage ?? string.Empty).Should().NotContain("NHAT-KY-CUA-NGUOI-KHAC");
        result.ErrorMessage.Should().NotBeNull("không còn nhật ký nào của người gọi để phân tích");
    }

    [Fact]
    public async Task JournalReview_OwnPortfolioWithJournalOfAnotherUser_OnlyOwnJournalsReachTheContext()
    {
        var repo = new Mock<ITradeJournalRepository>();
        repo.Setup(r => r.GetByPortfolioIdAsync("pf-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Journal(Caller, "pf-1", "NHAT-KY-HOP-LE"),
                Journal("other-user", "pf-1", "NHAT-KY-KHONG-THUOC"),
            });

        var result = await JournalReview(BuildService(repo), "pf-1");

        result.ErrorMessage.Should().BeNull();
        result.UserMessage.Should().Contain("NHAT-KY-HOP-LE").And.NotContain("NHAT-KY-KHONG-THUOC");
    }
}
