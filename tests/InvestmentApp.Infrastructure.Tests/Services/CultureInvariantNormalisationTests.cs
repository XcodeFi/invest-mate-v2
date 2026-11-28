using System.Globalization;
using System.Net;
using System.Text;
using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services.Hmoney;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Chuẩn hoá mã cổ phiếu và khoá (trạng thái, loại lệnh) không được phụ thuộc văn hoá của máy: dưới tr-TR, "vib".ToUpper() ra "VİB"
/// (chữ I có chấm) và "INPROGRESS".ToLower() ra "ınprogress", nên mã không khớp dữ liệu và các so sánh trạng thái vỡ.
/// </summary>
public class CultureInvariantNormalisationTests
{
    private static T UnderTurkish<T>(Func<T> action)
    {
        var old = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try { return action(); }
        finally { CultureInfo.CurrentCulture = old; }
    }

    // ───── hành vi ─────

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Urls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri?.ToString() ?? "");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task the_comprehensive_provider_requests_VIB_not_the_dotted_I_under_a_turkish_culture()
    {
        var handler = new RecordingHandler();
        var provider = new HmoneyComprehensiveDataProvider(
            new HttpClient(handler), Mock.Of<ILogger<HmoneyComprehensiveDataProvider>>(),
            Options.Create(new MarketDataProviderOptions { BaseUrl = "https://api-test.example.com" }));
        var old = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            await provider.GetComprehensiveDataAsync("vib");
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }

        handler.Urls.Should().NotBeEmpty();
        handler.Urls.Should().OnlyContain(u => !u.Contains("VİB") && !u.Contains("vıb"), "\"vib\" phải thành \"VIB\", không phải \"VİB\"");
        handler.Urls.Should().Contain(u => u.Contains("VIB"));
    }

    [Fact]
    public void domain_entities_normalise_the_symbol_to_VIB_under_a_turkish_culture()
    {
        var symbols = UnderTurkish(() => new[]
        {
            new Trade("p1", "vib", TradeType.BUY, 100, 30_000m).Symbol,
            new TradePlan("u1", "vib", "Buy", 100m, 90m, 130m, 100, thesis: "Luận điểm mẫu đủ dài cho test văn hoá tr-TR").Symbol,
            CorporateAction.CashDividend("p1", "u1", "vib", 10m, new DateTime(2026, 6, 10), null, 5m).Symbol,
            new InvestmentApp.Domain.ValueObjects.StockSymbol("vib").Value,
        });

        symbols.Should().OnlyContain(s => s == "VIB");
    }

    // ───── rào chắn: không để kiểu gọi này quay lại ─────

    private static string SrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "InvestmentApp.Domain"))) dir = dir.Parent;
        return dir == null ? throw new InvalidOperationException("Không tìm thấy thư mục src") : Path.Combine(dir.FullName, "src");
    }

    [Fact]
    public void no_culture_sensitive_ToUpper_or_ToLower_remains_in_src()
    {
        // .ToUpper() / .ToLower() không tham số chạy theo văn hoá của máy. Mọi chuẩn hoá mã/khoá phải dùng *Invariant.
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(SrcRoot(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            // Quét cả file (không theo dòng): bỏ chú thích và nội dung chuỗi, giữ phần mã trong lỗ nội suy; bắt cả khoảng trắng và tách dòng
            foreach (var line in CaseConversionScanner.FindCultureSensitiveCalls(File.ReadAllText(file)))
                offenders.Add($"{Path.GetRelativePath(SrcRoot(), file)}:{line}");
        }

        offenders.Should().BeEmpty("dùng ToUpperInvariant()/ToLowerInvariant() cho mã cổ phiếu và khoá:\n" + string.Join("\n", offenders));
    }
}
