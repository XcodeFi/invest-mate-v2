using System.Globalization;
using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Live smoke test — hit real 24hmoney.vn. Skipped by default (network access + flaky external dep).
/// Chạy manual để verify parser vẫn match HTML structure hiện tại:
///   dotnet test --filter "FullyQualifiedName~HmoneyGoldPriceProviderLiveSmoke" -e HMONEY_GOLD_SMOKE=1
/// </summary>
public class HmoneyGoldPriceProviderLiveSmoke
{
    private readonly ITestOutputHelper _output;

    public HmoneyGoldPriceProviderLiveSmoke(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Live_GetPricesAsync_ReturnsValidData()
    {
        if (Environment.GetEnvironmentVariable("HMONEY_GOLD_SMOKE") != "1")
        {
            _output.WriteLine("Skipped — set HMONEY_GOLD_SMOKE=1 to run.");
            return;
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; invest-mate-test)");
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var provider = new HmoneyGoldPriceProvider(
            httpClient,
            new MemoryCache(new MemoryCacheOptions()),
            Mock.Of<ILogger<HmoneyGoldPriceProvider>>(),
            Options.Create(new GoldPriceProviderOptions()));

        var prices = await provider.GetPricesAsync(CancellationToken.None);

        _output.WriteLine($"Fetched {prices.Count} prices:");
        foreach (var p in prices)
        {
            _output.WriteLine($"  {p.Brand} {p.Type}: Mua {p.BuyPrice.ToString("N0", CultureInfo.InvariantCulture)} / Bán {p.SellPrice.ToString("N0", CultureInfo.InvariantCulture)}");
        }

        // Sanity: at least SJC Miếng + Nhẫn present, prices in expected VND range
        prices.Should().NotBeEmpty();
        prices.Should().Contain(p => p.Brand == GoldBrand.SJC && p.Type == GoldType.Mieng);
        prices.Should().Contain(p => p.Brand == GoldBrand.SJC && p.Type == GoldType.Nhan);
        prices.Should().OnlyContain(p => p.BuyPrice >= 50_000_000m && p.BuyPrice <= 500_000_000m);
        prices.Should().OnlyContain(p => p.Type == GoldType.Mieng || p.Type == GoldType.Nhan);
    }
}
