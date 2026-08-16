using System.Net;
using System.Text;
using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Đo ngày 2026-08-16 trên api-finance-t19.24hmoney.vn, mã HAH: type 3/4/5/6 đều trả CÙNG một
/// cửa sổ 2026-06-08 → 2026-08-14 (67 ngày), lần lượt 50/26/11/4 điểm. Nghĩa là `type` chỉ còn
/// chọn ĐỘ MỊN của thanh nến, không còn chọn KHOẢNG. Xin khoảng dài hơn mà nhận được ít điểm hơn.
/// Khi upstream đổi tiếp, đo lại bằng curl rồi sửa cả nhận định này lẫn phép chọn type.
/// </summary>
public class HmoneyMarketDataProviderHistoryTests
{
    private static string GraphResponse(int dayCount, DateTime lastDate)
    {
        var points = Enumerable.Range(0, dayCount).Select(i =>
        {
            var date = lastDate.AddDays(-(dayCount - 1 - i));
            var epoch = new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeSeconds();
            return "{\"x\":" + epoch + ",\"y\":45.9,\"z\":1000}";
        });

        return "{\"message\":\"success\",\"status\":200,\"data\":{\"basic_price\":45.8,\"points\":["
            + string.Join(",", points) + "]}}";
    }

    private static (HmoneyMarketDataProvider Provider, RecordingHttpHandler Handler) CreateProvider(string body)
    {
        var handler = new RecordingHttpHandler(body);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api-test.example.com") };

        var provider = new HmoneyMarketDataProvider(
            httpClient,
            new MemoryCache(new MemoryCacheOptions()),
            new Mock<ILogger<HmoneyMarketDataProvider>>().Object,
            Options.Create(new MarketDataProviderOptions { BaseUrl = "https://api-test.example.com" }));

        return (provider, handler);
    }

    [Theory]
    [InlineData(30)]    // 1 tháng
    [InlineData(90)]    // 3 tháng
    [InlineData(365)]   // 1 năm
    [InlineData(1825)]  // 5 năm
    public async Task GetHistoricalPricesAsync_MoiKhoangDaiHonMotNgay_DeuXinChuoiNgayDayNhat(int days)
    {
        var (provider, handler) = CreateProvider(GraphResponse(60, DateTime.UtcNow.Date));

        await provider.GetHistoricalPricesAsync("HAH", DateTime.UtcNow.AddDays(-days), DateTime.UtcNow);

        handler.LastUrl.Should().Contain("type=3",
            "type chỉ còn chọn độ mịn thanh nến; type=4/5/6 chỉ làm thưa dữ liệu trong cùng cửa sổ");
    }

    [Fact]
    public async Task GetHistoricalPricesAsync_KhoangMotNgay_VanXinIntraday()
    {
        var (provider, handler) = CreateProvider(GraphResponse(60, DateTime.UtcNow.Date));

        await provider.GetHistoricalPricesAsync("HAH", DateTime.UtcNow.AddHours(-6), DateTime.UtcNow);

        handler.LastUrl.Should().Contain("type=1");
    }

    [Fact]
    public async Task GetHistoricalPricesAsync_XinMotNamSauKhiXinMotThang_KhongBiCacheCuaKhoangHepCatMat()
    {
        var (provider, _) = CreateProvider(GraphResponse(60, DateTime.UtcNow.Date));

        var oneMonth = await provider.GetHistoricalPricesAsync("HAH", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var oneYear = await provider.GetHistoricalPricesAsync("HAH", DateTime.UtcNow.AddDays(-365), DateTime.UtcNow);

        oneMonth.Count.Should().BeLessThan(60, "khoảng một tháng phải bị lọc bớt");
        oneYear.Count.Should().Be(60,
            "cache phải giữ chuỗi CHƯA lọc, nếu không lần xin hẹp trước sẽ cắt mất lần xin rộng sau");
    }

    [Fact]
    public async Task GetHistoricalPricesAsync_GoiLaiCungDoMin_ChiGoiUpstreamMotLan()
    {
        var (provider, handler) = CreateProvider(GraphResponse(60, DateTime.UtcNow.Date));

        await provider.GetHistoricalPricesAsync("HAH", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        await provider.GetHistoricalPricesAsync("HAH", DateTime.UtcNow.AddDays(-365), DateTime.UtcNow);

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetHistoricalPricesAsync_NguonTraChuoiRong_KhongCacheDeLanSauConDuongThu()
    {
        var (provider, handler) = CreateProvider(
            "{\"message\":\"success\",\"status\":200,\"data\":{\"basic_price\":0,\"points\":[]}}");

        await provider.GetHistoricalPricesAsync("VNINDEX", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        await provider.GetHistoricalPricesAsync("VNINDEX", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        handler.CallCount.Should().Be(2,
            "chuỗi rỗng có thể chỉ là trục trặc nhất thời; cache nó 60 giây là tự khoá mình khỏi lần thử sau");
    }

    private class RecordingHttpHandler(string body) : HttpMessageHandler
    {
        public string LastUrl { get; private set; } = "";
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri?.ToString() ?? "";
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
