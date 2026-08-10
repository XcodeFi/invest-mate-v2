using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketData.Queries.GetCompanyFundamentals;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace InvestmentApp.Application.Tests.MarketData;

/// <summary>
/// Điểm sống còn của query này là <c>UnavailableSections</c>: provider 24hmoney gộp ~9 lệnh gọi
/// HTTP, phần nào hỏng thì trả rỗng chứ không báo lỗi. Rỗng KHÔNG được hiểu là bằng không — nếu
/// agent đọc rỗng thành 0 thì nó viết hồ sơ từ khoảng trống và sinh ra hồ sơ qua được cổng mà
/// không có nội dung thật.
/// </summary>
public class GetCompanyFundamentalsQueryHandlerTests
{
    private readonly Mock<IComprehensiveStockDataProvider> _provider = new();

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private GetCompanyFundamentalsQueryHandler Sut() => new(_provider.Object, _cache);

    private void Returns(ComprehensiveStockData? data, string symbol = "HPG") =>
        _provider.Setup(p => p.GetComprehensiveDataAsync(symbol, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

    [Fact]
    public async Task WhenProviderReturnsNull_ShouldThrowKeyNotFound()
    {
        Returns(null);

        var act = () => Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task WhenCompanyAndIndicatorsBothNull_ShouldThrowRatherThanReturnEmpty()
    {
        // Trả 200 với mọi phần rỗng là tệ hơn 404: agent không phân biệt được "mã không có dữ liệu"
        // với "doanh nghiệp không có doanh thu".
        Returns(new ComprehensiveStockData { Symbol = "HPG" });

        var act = () => Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*không lấy được dữ liệu doanh nghiệp*");
    }

    [Fact]
    public async Task WhenCompanyAndIndicatorsAreEmptyShells_ShouldThrowNotFound()
    {
        // Mã sai (ZZZZ) trên provider thật KHÔNG trả null: nó trả về đủ hai object mà mọi field đều
        // null. Chấm theo `== null` thì cửa 404 là code chết, và agent gõ sai mã nhận 200 với hồ sơ
        // trống — đúng cái nó cần biết là "không có dữ liệu" thì lại đọc thành "mọi số bằng 0".
        Returns(new ComprehensiveStockData
        {
            Symbol = "ZZZZ",
            Company = new CompanyOverview(),
            Indicators = new FinanceIndicators()
        }, "ZZZZ");

        var act = () => Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "ZZZZ" }, default);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*không lấy được dữ liệu doanh nghiệp*");
    }

    [Fact]
    public async Task EmptyCompanyShell_IsDroppedAndFlagged_WhenIndicatorsHaveContent()
    {
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Company = new CompanyOverview(),
            Indicators = new FinanceIndicators { PE = 12.3m }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.Company.Should().BeNull("vỏ rỗng không được đi tiếp — FE sẽ render một khối trắng");
        dto.UnavailableSections.Should().Contain("company");
    }

    [Fact]
    public async Task CompanyWithOnlyIndustry_CountsAsPresent()
    {
        // HPG thật: không có tên công ty, nhưng có ngành + số cổ phiếu. Đó là thông tin dùng được.
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Company = new CompanyOverview { Industry = "Thép và sản phẩm thép", ListedShares = 8_442_964_520 }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.Company.Should().NotBeNull();
        dto.UnavailableSections.Should().NotContain("company");
    }

    [Fact]
    public async Task WhenIncomeStatementsEmpty_ShouldFlagUnavailableSection()
    {
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Indicators = new FinanceIndicators { PE = 12.3m, ROE = 18.2m }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.UnavailableSections.Should().Contain("incomeStatements");
        dto.UnavailableSections.Should().Contain("peers");
        dto.UnavailableSections.Should().Contain("company");
        dto.Indicators!.PE.Should().Be(12.3m);
    }

    [Fact]
    public async Task WhenCompanyPresentButIndicatorsNull_ShouldFlagIndicatorsAndNotThrow()
    {
        // Chỉ cần MỘT trong hai phần lõi là đủ để trả dữ liệu; phần thiếu phải nằm trong danh sách
        // chứ không im lặng thành null để FE render thành 0.
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Company = new CompanyOverview { CompanyName = "Tập đoàn Hòa Phát", Industry = "Thép và sản phẩm thép" }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.UnavailableSections.Should().Contain("indicators");
        dto.UnavailableSections.Should().NotContain("company");
        dto.Company!.Industry.Should().Be("Thép và sản phẩm thép");
    }

    [Fact]
    public async Task WhenEverySectionPresent_UnavailableSectionsShouldBeEmpty()
    {
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Company = new CompanyOverview { CompanyName = "Tập đoàn Hòa Phát" },
            Indicators = new FinanceIndicators { PE = 12.3m },
            IncomeStatements = { new IncomeStatementItem { Period = "Q1/2026", Revenue = 35_000m } },
            Peers = { new PeerStock { Symbol = "HSG" } },
            DividendEvents = { new DividendEvent { EventType = "cash", Value = 500m } },
            BusinessPlan = new CompanyPlan { Year = 2026, RevenuePlan = 150_000m },
            AnalystReports = { new AnalystReport { Title = "Khuyến nghị mua" } },
            ForeignTrading = { new ForeignTradingDay { Date = "2026-08-10", NetVolume = 1_000m } }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.UnavailableSections.Should().BeEmpty();
    }

    [Fact]
    public async Task ItemsWithNoContent_CountAsUnavailable_AndAreDroppedFromPayload()
    {
        // 24hmoney thật trả về đúng ca này cho HPG: 10 phần tử cổ tức mà mọi field đều null. Đếm
        // theo Count thì section coi như có dữ liệu, và UI render 10 dòng gạch ngang — loại "trông
        // như dữ liệu nhưng không mang gì" chính là thứ danh sách unavailable tồn tại để chặn.
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Indicators = new FinanceIndicators { PE = 12.3m },
            DividendEvents =
            {
                new DividendEvent(), new DividendEvent()
            },
            Peers = { new PeerStock { Symbol = "   " } },
            IncomeStatements = { new IncomeStatementItem() }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.UnavailableSections.Should().Contain("dividendEvents");
        dto.UnavailableSections.Should().Contain("peers");
        dto.UnavailableSections.Should().Contain("incomeStatements");
        dto.DividendEvents.Should().BeEmpty();
        dto.Peers.Should().BeEmpty();
        dto.IncomeStatements.Should().BeEmpty();
    }

    [Fact]
    public async Task ItemsWithPartialContent_AreKept()
    {
        // Chỉ bỏ phần tử RỖNG HẲN. Một sự kiện cổ tức chỉ có ngày GDKHQ vẫn là thông tin thật.
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Indicators = new FinanceIndicators { PE = 12.3m },
            DividendEvents =
            {
                new DividendEvent(), new DividendEvent { ExDate = "2026-06-30" }
            }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.UnavailableSections.Should().NotContain("dividendEvents");
        dto.DividendEvents.Should().HaveCount(1);
        dto.DividendEvents[0].ExDate.Should().Be("2026-06-30");
    }


    [Fact]
    public async Task SecondCallForSameSymbol_ComesFromCache_NotFromProvider()
    {
        // Một lần gọi provider là ~9 request HTTP ra 24hmoney. Panel + tool MCP cùng gọi endpoint này,
        // và PR trước vừa phải thêm cache cho đúng provider này vì lý do y hệt.
        Returns(new ComprehensiveStockData { Symbol = "HPG", Indicators = new FinanceIndicators { PE = 12.3m } });

        await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);
        var second = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = " hpg " }, default);

        _provider.Verify(p => p.GetComprehensiveDataAsync("HPG", It.IsAny<CancellationToken>()), Times.Once);
        second.Indicators!.PE.Should().Be(12.3m);
    }

    [Fact]
    public async Task FailedLookup_IsNotCached()
    {
        // Cache cả ca lỗi là đóng băng một lỗi mạng nhất thời thành "mã không có dữ liệu" suốt TTL.
        Returns(null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default));

        Returns(new ComprehensiveStockData { Symbol = "HPG", Indicators = new FinanceIndicators { PE = 9m } });
        var retry = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        retry.Indicators!.PE.Should().Be(9m);
    }

    [Fact]
    public async Task EmptyBusinessPlanShell_IsDroppedAndFlagged()
    {
        // Vỏ rỗng của CompanyPlan từng đi thẳng vào DTO trong khi Company/Indicators đã được chấm
        // theo nội dung — panel hiện "Năm : doanh thu — tỷ" như thể có kế hoạch thật.
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Indicators = new FinanceIndicators { PE = 12.3m },
            BusinessPlan = new CompanyPlan()
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.BusinessPlan.Should().BeNull();
        dto.UnavailableSections.Should().Contain("businessPlan");
    }

    [Fact]
    public async Task CompanyWhoseOnlyContentIsAListOfEmptyShells_CountsAsEmpty()
    {
        // Danh sách 2 cổ đông mà mọi field null không làm khối công ty "có dữ liệu": nếu tính là có,
        // FE hiện hai dòng cổ đông gạch ngang — đúng loại "trông như dữ liệu nhưng không mang gì".
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Company = new CompanyOverview { MajorShareholders = { new Shareholder(), new Shareholder() } },
            Indicators = new FinanceIndicators { PE = 12.3m }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.Company.Should().BeNull();
        dto.UnavailableSections.Should().Contain("company");
    }

    [Fact]
    public async Task ShareholderWithRealPercentage_CountsAsContent()
    {
        // Đối xứng với test trên: Percentage là decimal KHÔNG nullable nên phải cẩn thận — một cổ đông
        // có tên và tỷ lệ thật thì khối công ty phải được coi là có dữ liệu.
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Company = new CompanyOverview
            {
                MajorShareholders = { new Shareholder { Name = "Trần Đình Long", Percentage = 26.08m } }
            }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = "HPG" }, default);

        dto.Company.Should().NotBeNull();
        dto.UnavailableSections.Should().NotContain("company");
    }

    [Fact]
    public async Task ShouldNormalizeSymbolBeforeCallingProvider()
    {
        Returns(new ComprehensiveStockData
        {
            Symbol = "HPG",
            Indicators = new FinanceIndicators { PE = 1m }
        });

        var dto = await Sut().Handle(new GetCompanyFundamentalsQuery { Symbol = " hpg " }, default);

        _provider.Verify(p => p.GetComprehensiveDataAsync("HPG", It.IsAny<CancellationToken>()), Times.Once);
        dto.Symbol.Should().Be("HPG");
    }
}
