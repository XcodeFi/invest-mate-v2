using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalAnalysis;
using InvestmentApp.Application.MarketData.Queries.GetTechnicalSeries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

/// <summary>
/// Tham số <c>months</c> của hai endpoint chỉ báo phải đi tới query: trang Dòng thời gian mã xin dài hơn khoảng hiển thị để chỉ báo có
/// đủ dữ liệu khởi động. Test ở tầng handler vẫn xanh dù controller thôi truyền tham số, nên khoá ở đây.
/// </summary>
public class MarketDataControllerMonthsTests
{
    private readonly Mock<IMediator> _mediator = new();

    private MarketDataController Sut() => new(_mediator.Object);

    [Theory]
    [InlineData(24, 24)]
    [InlineData(36, 36)]
    [InlineData(null, 12)]
    public async Task Series_ForwardsMonthsToTheQuery_AndKeepsTwelveByDefault(int? months, int expected)
    {
        GetTechnicalSeriesQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetTechnicalSeriesQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<TechnicalSeriesResult>, CancellationToken>((q, _) => sent = (GetTechnicalSeriesQuery)q)
            .ReturnsAsync(new TechnicalSeriesResult());

        var result = await Sut().GetTechnicalSeries("FPT", months);

        result.Should().BeOfType<OkObjectResult>();
        sent!.Symbol.Should().Be("FPT");
        sent.Months.Should().Be(expected);
    }

    [Theory]
    [InlineData(15, 15)]
    [InlineData(null, 12)]
    public async Task Analysis_ForwardsMonthsToTheQuery_AndKeepsTwelveByDefault(int? months, int expected)
    {
        GetTechnicalAnalysisQuery? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<GetTechnicalAnalysisQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<TechnicalAnalysisResult>, CancellationToken>((q, _) => sent = (GetTechnicalAnalysisQuery)q)
            .ReturnsAsync(new TechnicalAnalysisResult());

        var result = await Sut().GetTechnicalAnalysis("FPT", months);

        result.Should().BeOfType<OkObjectResult>();
        sent!.Symbol.Should().Be("FPT");
        sent.Months.Should().Be(expected);
    }
}
