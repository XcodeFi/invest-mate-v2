using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Ngưỡng cắt lỗ lưu giá tuyệt đối tại lúc đặt. Sau ngày GDKHQ giá thị trường bị điều chỉnh
/// giảm, nên nếu không điều chỉnh ngưỡng thì cắt lỗ sẽ bắn nhầm hàng loạt.
/// </summary>
public class StopLossCorporateActionTests
{
    private readonly Mock<ITradeRepository> _trades = new();
    private readonly Mock<IStockPriceRepository> _prices = new();
    private readonly Mock<IMarketIndexRepository> _indices = new();
    private readonly Mock<IMarketDataProvider> _marketData = new();
    private readonly Mock<IStopLossTargetRepository> _slRepo = new();
    private readonly Mock<ICorporateActionRepository> _actions = new();

    private static readonly DateTime SetAt = new(2026, 1, 5);
    private static readonly DateTime Ex = new(2026, 6, 10);

    private PriceSnapshotJobService Sut() => new(
        _trades.Object, _prices.Object, _indices.Object, _marketData.Object,
        _slRepo.Object, _actions.Object, NullLogger<PriceSnapshotJobService>.Instance);

    /// <summary>
    /// Giá vào 25.000, cắt lỗ 22.000, mục tiêu 40.000. Mốc điều chỉnh là <c>UpdatedAt</c> —
    /// lần sửa gần nhất, chứ không phải lúc tạo: sửa cắt lỗ SAU sự kiện quyền nghĩa là
    /// người dùng đã nhập theo giá mới, điều chỉnh thêm lần nữa sẽ giấu mất lệnh cắt lỗ thật.
    /// </summary>
    private static StopLossTarget HpgTarget(DateTime? updatedAt = null)
    {
        var target = new StopLossTarget("t1", "p1", "u1", "HPG", 25_000m, 22_000m, 40_000m);
        typeof(StopLossTarget).GetProperty(nameof(StopLossTarget.UpdatedAt))!
            .SetValue(target, updatedAt ?? SetAt);
        return target;
    }

    private void SetupJob(decimal closePrice, CorporateAction[] actions, DateTime? updatedAt = null)
    {
        _trades.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new Trade("p1", "HPG", TradeType.BUY, 1000, 25_000m) });

        _marketData.Setup(m => m.GetBatchPricesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, StockPriceData>
            {
                ["HPG"] = new StockPriceData
                {
                    Symbol = "HPG",
                    Date = new DateTime(2026, 6, 15),
                    Open = closePrice,
                    High = closePrice,
                    Low = closePrice,
                    Close = closePrice,
                    Volume = 1000
                }
            });

        _slRepo.Setup(r => r.GetUntriggeredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { HpgTarget(updatedAt) });

        _actions.Setup(r => r.GetByPortfolioIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions);
    }

    [Fact]
    public async Task SauCoTucCoPhieu_GiaDaDieuChinh_ThiKhongKichHoatCatLoNham()
    {
        // Giá 23.100 sau điều chỉnh ≈ 30.030 trước điều chỉnh — vị thế vẫn đang lãi.
        // Ngưỡng cắt lỗ điều chỉnh = 22.000 / 1,3 ≈ 16.923 → không được bắn.
        SetupJob(23_100m, new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null) });

        var result = await Sut().RunAsync();

        result.StopLossTriggered.Should().Be(0);
        _slRepo.Verify(r => r.UpdateAsync(It.IsAny<StopLossTarget>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GiaXuyenThungNguongDaDieuChinh_ThiVanKichHoat()
    {
        SetupJob(16_000m, new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null) });

        var result = await Sut().RunAsync();

        result.StopLossTriggered.Should().Be(1);
    }

    [Fact]
    public async Task KhongCoSuKienQuyen_ThiGiuNguyenHanhViCu()
    {
        SetupJob(21_000m, Array.Empty<CorporateAction>());

        var result = await Sut().RunAsync();

        result.StopLossTriggered.Should().Be(1);
    }

    [Fact]
    public async Task SuaCatLoSauSuKienQuyen_ThiKhongDieuChinhChongLen()
    {
        // Người dùng sửa cắt lỗ về 22.000 vào ngày 2026-07-01, tức SAU ngày GDKHQ —
        // con số đó đã theo giá mới. Nếu vẫn lấy mốc CreatedAt mà điều chỉnh tiếp,
        // ngưỡng tụt còn ~16.923 và lệnh cắt lỗ thật bị giấu mất.
        SetupJob(21_000m,
            new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null) },
            updatedAt: new DateTime(2026, 7, 1));

        var result = await Sut().RunAsync();

        result.StopLossTriggered.Should().Be(1);
    }

    [Fact]
    public async Task MucTieuCungDuocDieuChinh_KhongChotLaiNham()
    {
        // Mục tiêu 40.000 điều chỉnh còn ≈ 30.769. Giá 31.000 → đã đạt mục tiêu thật.
        SetupJob(31_000m, new[] { CorporateAction.StockDividend("p1", "u1", "HPG", 100, 130, Ex, null) });

        var result = await Sut().RunAsync();

        result.TargetsTriggered.Should().Be(1);
        result.StopLossTriggered.Should().Be(0);
    }
}
