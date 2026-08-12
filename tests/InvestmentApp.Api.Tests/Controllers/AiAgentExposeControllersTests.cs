using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.DeleteJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;
using InvestmentApp.Application.JournalEntries.Queries.GetJournalEntriesBySymbol;
using InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;
using InvestmentApp.Application.Journals.Commands.CreateJournal;
using InvestmentApp.Application.Journals.Commands.DeleteJournal;
using InvestmentApp.Application.Journals.Commands.UpdateJournal;
using InvestmentApp.Application.Journals.Queries.GetJournalByTrade;
using InvestmentApp.Application.Journals.Queries.GetJournals;
using InvestmentApp.Application.Journals.Queries.GetTradesPendingReview;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using InvestmentApp.Application.Portfolios.Queries.GetAllPortfolios;
using InvestmentApp.Application.TradePlans.Queries.GetActivePositions;
using InvestmentApp.Application.Watchlists.Commands.AddWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.CreateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.DeleteWatchlist;
using InvestmentApp.Application.Watchlists.Commands.ImportVn30;
using InvestmentApp.Application.Watchlists.Commands.RemoveWatchlistItem;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlist;
using InvestmentApp.Application.Watchlists.Commands.UpdateWatchlistItem;
using InvestmentApp.Application.Watchlists.Dtos;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlistDetail;
using InvestmentApp.Application.Watchlists.Queries.GetWatchlists;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

/// <summary>
/// Wiring tests cho 5 controller mở rộng agent surface. Handler đã có test ở Application layer →
/// ở đây chỉ verify: dispatch đúng command/query, inject UserId từ claim "sub", bind route params,
/// giữ đúng mã trạng thái + Created Location trỏ về agent surface.
/// </summary>
public class AiAgentExposeControllersTests
{
    private readonly Mock<IMediator> _mediator = new();

    private static T WithApiKeyClaim<T>(T controller, string userId) where T : ControllerBase
    {
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId) }, "ApiKey");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    // ---- Positions ----

    [Fact]
    public async Task Positions_Get_InjectsUserIdAndPortfolioId()
    {
        GetActivePositionsQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetActivePositionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<List<ActivePositionDto>>, CancellationToken>((q, _) => sent = (GetActivePositionsQuery)q)
            .ReturnsAsync(new List<ActivePositionDto>());

        var sut = WithApiKeyClaim(new AiAgentPositionsController(_mediator.Object), "user-1");
        var result = await sut.GetActivePositions("pf-9");

        result.Should().BeOfType<OkObjectResult>();
        sent!.UserId.Should().Be("user-1");
        sent.PortfolioId.Should().Be("pf-9");
    }

    // ---- Watchlists ----

    [Fact]
    public async Task Watchlist_Create_ReturnsCreated_WithAgentSurfaceLocation_AndUserId()
    {
        CreateWatchlistCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateWatchlistCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<WatchlistDto>, CancellationToken>((c, _) => sent = (CreateWatchlistCommand)c)
            .ReturnsAsync(new WatchlistDto { Id = "wl-1", Name = "Tech" });

        var sut = WithApiKeyClaim(new AiAgentWatchlistsController(_mediator.Object), "user-1");
        var result = await sut.Create(new CreateWatchlistCommand { Name = "Tech" }) as CreatedResult;

        result.Should().NotBeNull();
        result!.Location.Should().Be("/api/v1/ai/agent/watchlists/wl-1");
        sent!.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Watchlist_AddItem_BindsWatchlistIdFromRoute_AndUserId()
    {
        AddWatchlistItemCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<AddWatchlistItemCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<WatchlistDetailDto>, CancellationToken>((c, _) => sent = (AddWatchlistItemCommand)c)
            .ReturnsAsync(new WatchlistDetailDto());

        var sut = WithApiKeyClaim(new AiAgentWatchlistsController(_mediator.Object), "user-1");
        var result = await sut.AddItem("wl-1", new AddWatchlistItemCommand { Symbol = "VNM" });

        result.Should().BeOfType<OkObjectResult>();
        sent!.WatchlistId.Should().Be("wl-1");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Watchlist_UpdateItem_BindsWatchlistIdAndSymbolFromRoute()
    {
        UpdateWatchlistItemCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpdateWatchlistItemCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<WatchlistDetailDto>, CancellationToken>((c, _) => sent = (UpdateWatchlistItemCommand)c)
            .ReturnsAsync(new WatchlistDetailDto());

        var sut = WithApiKeyClaim(new AiAgentWatchlistsController(_mediator.Object), "user-1");
        await sut.UpdateItem("wl-1", "VNM", new UpdateWatchlistItemCommand { Note = "x" });

        sent!.WatchlistId.Should().Be("wl-1");
        sent.Symbol.Should().Be("VNM");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Watchlist_RemoveItem_BuildsCommandFromRoute()
    {
        RemoveWatchlistItemCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<RemoveWatchlistItemCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<WatchlistDetailDto>, CancellationToken>((c, _) => sent = (RemoveWatchlistItemCommand)c)
            .ReturnsAsync(new WatchlistDetailDto());

        var sut = WithApiKeyClaim(new AiAgentWatchlistsController(_mediator.Object), "user-1");
        await sut.RemoveItem("wl-1", "VNM");

        sent!.WatchlistId.Should().Be("wl-1");
        sent.Symbol.Should().Be("VNM");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Watchlist_Delete_BuildsCommand_ReturnsNoContent()
    {
        DeleteWatchlistCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<DeleteWatchlistCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Unit>, CancellationToken>((c, _) => sent = (DeleteWatchlistCommand)c)
            .ReturnsAsync(Unit.Value);

        var sut = WithApiKeyClaim(new AiAgentWatchlistsController(_mediator.Object), "user-1");
        var result = await sut.Delete("wl-1");

        result.Should().BeOfType<NoContentResult>();
        sent!.Id.Should().Be("wl-1");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Watchlist_Update_BindsId_ReturnsNoContent()
    {
        UpdateWatchlistCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpdateWatchlistCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Unit>, CancellationToken>((c, _) => sent = (UpdateWatchlistCommand)c)
            .ReturnsAsync(Unit.Value);

        var sut = WithApiKeyClaim(new AiAgentWatchlistsController(_mediator.Object), "user-1");
        var result = await sut.Update("wl-1", new UpdateWatchlistCommand { Name = "New" });

        result.Should().BeOfType<NoContentResult>();
        sent!.Id.Should().Be("wl-1");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Watchlist_GetAll_InjectsUserId()
    {
        GetWatchlistsQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetWatchlistsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<List<WatchlistDto>>, CancellationToken>((q, _) => sent = (GetWatchlistsQuery)q)
            .ReturnsAsync(new List<WatchlistDto>());

        var sut = WithApiKeyClaim(new AiAgentWatchlistsController(_mediator.Object), "user-1");
        await sut.GetAll();

        sent!.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Watchlist_GetDetail_InjectsUserId()
    {
        GetWatchlistDetailQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetWatchlistDetailQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<WatchlistDetailDto>, CancellationToken>((q, _) => sent = (GetWatchlistDetailQuery)q)
            .ReturnsAsync(new WatchlistDetailDto());

        var sut = WithApiKeyClaim(new AiAgentWatchlistsController(_mediator.Object), "user-1");
        await sut.GetDetail("wl-1");

        sent!.Id.Should().Be("wl-1");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Watchlist_ImportVn30_InjectsUserId()
    {
        ImportVn30Command? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<ImportVn30Command>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<WatchlistDetailDto>, CancellationToken>((c, _) => sent = (ImportVn30Command)c)
            .ReturnsAsync(new WatchlistDetailDto());

        var sut = WithApiKeyClaim(new AiAgentWatchlistsController(_mediator.Object), "user-1");
        var result = await sut.ImportVn30(new ImportVn30Command());

        result.Should().BeOfType<OkObjectResult>();
        sent!.UserId.Should().Be("user-1");
    }

    // ---- JournalEntries ----

    [Fact]
    public async Task JournalEntry_Create_ReturnsCreated_WithAgentSurfaceLocation()
    {
        CreateJournalEntryCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (CreateJournalEntryCommand)c)
            .ReturnsAsync("je-1");

        var sut = WithApiKeyClaim(new AiAgentJournalEntriesController(_mediator.Object), "user-1");
        var result = await sut.CreateJournalEntry(
            new CreateJournalEntryCommand { Symbol = "VNM", EntryType = "Observation", Title = "t", Content = "c" }) as CreatedResult;

        result.Should().NotBeNull();
        result!.Location.Should().Be("/api/v1/ai/agent/journal-entries/je-1");
        sent!.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task JournalEntry_Update_False_Returns404()
    {
        _mediator.Setup(m => m.Send(It.IsAny<UpdateJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = WithApiKeyClaim(new AiAgentJournalEntriesController(_mediator.Object), "user-1");
        var result = await sut.UpdateJournalEntry("je-x", new UpdateJournalEntryCommand());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task JournalEntry_Update_True_BindsIdAndUserId_ReturnsNoContent()
    {
        UpdateJournalEntryCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpdateJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<bool>, CancellationToken>((c, _) => sent = (UpdateJournalEntryCommand)c)
            .ReturnsAsync(true);

        var sut = WithApiKeyClaim(new AiAgentJournalEntriesController(_mediator.Object), "user-1");
        var result = await sut.UpdateJournalEntry("je-1", new UpdateJournalEntryCommand());

        result.Should().BeOfType<NoContentResult>();
        sent!.Id.Should().Be("je-1");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task JournalEntry_Delete_False_Returns404()
    {
        _mediator.Setup(m => m.Send(It.IsAny<DeleteJournalEntryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = WithApiKeyClaim(new AiAgentJournalEntriesController(_mediator.Object), "user-1");
        var result = await sut.DeleteJournalEntry("je-x");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task JournalEntry_GetBySymbol_MissingSymbol_Returns400()
    {
        var sut = WithApiKeyClaim(new AiAgentJournalEntriesController(_mediator.Object), "user-1");
        var result = await sut.GetJournalEntries(symbol: "  ", from: null, to: null);

        result.Should().BeOfType<BadRequestObjectResult>();
        _mediator.Verify(m => m.Send(It.IsAny<GetJournalEntriesBySymbolQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JournalEntry_GetBySymbol_WithSymbol_InjectsUserId()
    {
        GetJournalEntriesBySymbolQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetJournalEntriesBySymbolQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<List<JournalEntryDto>>, CancellationToken>((q, _) => sent = (GetJournalEntriesBySymbolQuery)q)
            .ReturnsAsync(new List<JournalEntryDto>());

        var sut = WithApiKeyClaim(new AiAgentJournalEntriesController(_mediator.Object), "user-1");
        var result = await sut.GetJournalEntries(symbol: "VNM", from: null, to: null);

        result.Should().BeOfType<OkObjectResult>();
        sent!.Symbol.Should().Be("VNM");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task JournalEntry_PendingReview_InjectsUserId()
    {
        GetTradesPendingReviewQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetTradesPendingReviewQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<List<PendingReviewTradeDto>>, CancellationToken>((q, _) => sent = (GetTradesPendingReviewQuery)q)
            .ReturnsAsync(new List<PendingReviewTradeDto>());

        var sut = WithApiKeyClaim(new AiAgentJournalEntriesController(_mediator.Object), "user-1");
        await sut.GetPendingReview("pf-1");

        sent!.UserId.Should().Be("user-1");
        sent.PortfolioId.Should().Be("pf-1");
    }

    // ---- Journals ----

    [Fact]
    public async Task Journal_GetByTrade_Null_Returns404_AndInjectsTradeIdAndUserId()
    {
        GetJournalByTradeQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetJournalByTradeQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<JournalDto?>, CancellationToken>((q, _) => sent = (GetJournalByTradeQuery)q)
            .ReturnsAsync((JournalDto?)null);

        var sut = WithApiKeyClaim(new AiAgentJournalsController(_mediator.Object), "user-1");
        var result = await sut.GetJournalByTrade("t-x");

        result.Should().BeOfType<NotFoundObjectResult>();
        sent!.TradeId.Should().Be("t-x");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Journal_Create_ReturnsCreated_WithAgentSurfaceLocation()
    {
        CreateJournalCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<CreateJournalCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (CreateJournalCommand)c)
            .ReturnsAsync("j-1");

        var sut = WithApiKeyClaim(new AiAgentJournalsController(_mediator.Object), "user-1");
        var result = await sut.CreateJournal(
            new CreateJournalCommand { TradeId = "t-1", PortfolioId = "pf-1" }) as CreatedResult;

        result.Should().NotBeNull();
        result!.Location.Should().Be("/api/v1/ai/agent/journals/j-1");
        sent!.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Journal_Update_BindsId_ReturnsNoContent()
    {
        UpdateJournalCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpdateJournalCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Unit>, CancellationToken>((c, _) => sent = (UpdateJournalCommand)c)
            .ReturnsAsync(Unit.Value);

        var sut = WithApiKeyClaim(new AiAgentJournalsController(_mediator.Object), "user-1");
        var result = await sut.UpdateJournal("j-1", new UpdateJournalCommand());

        result.Should().BeOfType<NoContentResult>();
        sent!.Id.Should().Be("j-1");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Journal_Delete_BuildsCommand_ReturnsNoContent()
    {
        DeleteJournalCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<DeleteJournalCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Unit>, CancellationToken>((c, _) => sent = (DeleteJournalCommand)c)
            .ReturnsAsync(Unit.Value);

        var sut = WithApiKeyClaim(new AiAgentJournalsController(_mediator.Object), "user-1");
        var result = await sut.DeleteJournal("j-1");

        result.Should().BeOfType<NoContentResult>();
        sent!.Id.Should().Be("j-1");
        sent.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task Journal_GetJournals_InjectsUserId()
    {
        GetJournalsQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetJournalsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<IEnumerable<JournalDto>>, CancellationToken>((q, _) => sent = (GetJournalsQuery)q)
            .ReturnsAsync(new List<JournalDto>());

        var sut = WithApiKeyClaim(new AiAgentJournalsController(_mediator.Object), "user-1");
        await sut.GetJournals("pf-1");

        sent!.UserId.Should().Be("user-1");
        sent.PortfolioId.Should().Be("pf-1");
    }

    // ---- Symbol timeline ----

    [Fact]
    public async Task Symbol_Timeline_InjectsUserIdAndSymbol()
    {
        GetSymbolTimelineQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetSymbolTimelineQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<SymbolTimelineDto>, CancellationToken>((q, _) => sent = (GetSymbolTimelineQuery)q)
            .ReturnsAsync(new SymbolTimelineDto());

        var sut = WithApiKeyClaim(new AiAgentSymbolsController(_mediator.Object), "user-1");
        var result = await sut.GetSymbolTimeline("VNM", from: null, to: null);

        result.Should().BeOfType<OkObjectResult>();
        sent!.UserId.Should().Be("user-1");
        sent.Symbol.Should().Be("VNM");
    }

    // ---- Portfolios ----

    [Fact]
    public async Task Portfolios_Get_InjectsUserId()
    {
        GetAllPortfoliosQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetAllPortfoliosQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<List<PortfolioSummaryDto>>, CancellationToken>((q, _) => sent = (GetAllPortfoliosQuery)q)
            .ReturnsAsync(new List<PortfolioSummaryDto>());

        var sut = WithApiKeyClaim(new AiAgentPortfoliosController(_mediator.Object), "user-1");
        var result = await sut.GetPortfolios();

        result.Should().BeOfType<OkObjectResult>();
        sent!.UserId.Should().Be("user-1");
    }

    // ---- Fees ----

    private static Mock<IFeeCalculationService> FeeServiceMock()
    {
        var mock = new Mock<IFeeCalculationService>();
        mock.Setup(f => f.GetFeesSummary(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(new TradingFeesSummary { TransactionFee = new Money(150000, "VND") });
        mock.Setup(f => f.CalculateVAT(It.IsAny<Money>(), It.IsAny<string>()))
            .Returns(new Money(15000, "VND"));
        mock.Setup(f => f.CalculateSecuritiesTax(It.IsAny<Money>(), It.IsAny<SecurityType>(), It.IsAny<bool>()))
            .Returns((Money amt, SecurityType _, bool isBuy) => new Money(isBuy ? 0m : amt.Amount * 0.001m, "VND"));
        return mock;
    }

    [Fact]
    public void Fees_Calculate_Sell_ComputesPitTax()
    {
        var sut = WithApiKeyClaim(new AiAgentFeesController(_mediator.Object, FeeServiceMock().Object), "user-1");

        var result = sut.Calculate(new FeeCalculationRequest
        { Symbol = "HHV", TradeType = "Sell", Quantity = 100, Price = 1000000 }) as OkObjectResult;

        var resp = result!.Value as FeeCalculationResponse;
        resp!.Tax.Should().Be(100000);          // 0.1% of 100,000,000
        resp.TransactionFee.Should().Be(150000);
        resp.Vat.Should().Be(15000);
        resp.TotalFees.Should().Be(265000);
    }

    [Fact]
    public void Fees_Calculate_Buy_ZeroTax()
    {
        var sut = WithApiKeyClaim(new AiAgentFeesController(_mediator.Object, FeeServiceMock().Object), "user-1");

        var result = sut.Calculate(new FeeCalculationRequest
        { Symbol = "VNM", TradeType = "Buy", Quantity = 100, Price = 1000000 }) as OkObjectResult;

        var resp = result!.Value as FeeCalculationResponse;
        resp!.Tax.Should().Be(0);
    }

    [Fact]
    public void Fees_Calculate_NonPositiveAmount_Returns400()
    {
        var sut = WithApiKeyClaim(new AiAgentFeesController(_mediator.Object, FeeServiceMock().Object), "user-1");

        var result = sut.Calculate(new FeeCalculationRequest
        { Symbol = "VNM", TradeType = "Buy", Quantity = 0, Price = 1000000 });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // --- Lịch nghỉ giao dịch: bản ApiKey phải ngang giá bản JWT ---

    [Fact]
    public async Task MarketClosures_Get_DispatchesQueryWithClaimUserId()
    {
        GetMarketClosuresQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetMarketClosuresQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<MarketClosureYearDto>, CancellationToken>((q, _) => sent = (GetMarketClosuresQuery)q)
            .ReturnsAsync(new MarketClosureYearDto(2026, new List<MarketClosureMonthDto>()));
        var sut = WithApiKeyClaim(new AiAgentMarketClosuresController(_mediator.Object), "user-1");

        var result = await sut.Get(2026);

        result.Should().BeOfType<OkObjectResult>();
        sent!.UserId.Should().Be("user-1");
        sent.Year.Should().Be(2026);
    }

    [Fact]
    public async Task MarketClosures_Add_DispatchesCommandWithDatesAndNote()
    {
        AddMarketClosuresCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<AddMarketClosuresCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<AddMarketClosuresResult>, CancellationToken>((c, _) => sent = (AddMarketClosuresCommand)c)
            .ReturnsAsync(new AddMarketClosuresResult(2, 0, 0));
        var sut = WithApiKeyClaim(new AiAgentMarketClosuresController(_mediator.Object), "user-1");

        var result = await sut.Add(new AiAgentMarketClosuresController.AddRequest(
            new List<DateTime> { new(2026, 4, 30), new(2026, 5, 1) }, "Lễ 30/4"));

        result.Should().BeOfType<OkObjectResult>();
        sent!.UserId.Should().Be("user-1");
        sent.Dates.Should().HaveCount(2);
        sent.Note.Should().Be("Lễ 30/4");
    }

    [Fact]
    public async Task MarketClosures_Remove_KhongCoGiDeXoa_Returns404()
    {
        _mediator.Setup(m => m.Send(It.IsAny<RemoveMarketClosureCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = WithApiKeyClaim(new AiAgentMarketClosuresController(_mediator.Object), "user-1");

        var result = await sut.Remove(new DateTime(2026, 7, 7));

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task MarketClosures_Remove_XoaDuoc_Returns204()
    {
        _mediator.Setup(m => m.Send(It.IsAny<RemoveMarketClosureCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = WithApiKeyClaim(new AiAgentMarketClosuresController(_mediator.Object), "user-1");

        var result = await sut.Remove(new DateTime(2026, 4, 27));

        result.Should().BeOfType<NoContentResult>();
    }
}
