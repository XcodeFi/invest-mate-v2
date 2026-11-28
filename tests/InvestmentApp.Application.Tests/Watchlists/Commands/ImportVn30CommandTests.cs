using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Watchlists.Commands.ImportVn30;
using InvestmentApp.Domain.Constants;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Watchlists.Commands;

public class ImportVn30CommandTests
{
    [Fact]
    public async Task imports_exactly_the_symbols_of_the_shared_vn30_basket()
    {
        var repo = new Mock<IWatchlistRepository>();
        Watchlist? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Watchlist>(), It.IsAny<CancellationToken>()))
            .Callback<Watchlist, CancellationToken>((w, _) => saved = w)
            .Returns(Task.CompletedTask);

        await new ImportVn30CommandHandler(repo.Object).Handle(new ImportVn30Command { UserId = "u1" }, CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Items.Select(i => i.Symbol).Should().BeEquivalentTo(Vn30Basket.Symbols);
    }
}
