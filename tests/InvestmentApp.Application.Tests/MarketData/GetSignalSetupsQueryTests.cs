using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetSignalSetups;
using Moq;

namespace InvestmentApp.Application.Tests.MarketData;

public class GetSignalSetupsQueryTests
{
    [Fact]
    public async Task normalizes_the_symbol_and_returns_the_service_result()
    {
        var service = new Mock<ISignalSetupService>();
        var dto = new SignalSetupsDto { Symbol = "FPT" };
        service.Setup(s => s.GetSignalSetupsAsync("FPT", It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await new GetSignalSetupsQueryHandler(service.Object)
            .Handle(new GetSignalSetupsQuery { Symbol = "  fpt " }, CancellationToken.None);

        result.Should().BeSameAs(dto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task blank_symbol_is_refused_and_the_service_is_not_called(string symbol)
    {
        var service = new Mock<ISignalSetupService>();

        var act = () => new GetSignalSetupsQueryHandler(service.Object).Handle(new GetSignalSetupsQuery { Symbol = symbol }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        service.VerifyNoOtherCalls();
    }
}
