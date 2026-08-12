using FluentAssertions;
using Moq;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;
using InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;
using InvestmentApp.Application.MarketClosures.Queries.GetMarketClosures;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.MarketClosures;

public class MarketClosureHandlerTests
{
    private readonly Mock<IMarketClosureRepository> _repo = new();

    [Fact]
    public async Task Nhap_ca_dot_le_thi_dem_dung_so_them_moi()
    {
        _repo.Setup(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AddMarketClosuresCommandHandler(_repo.Object);
        var result = await handler.Handle(new AddMarketClosuresCommand("user1",
            new[] { new DateTime(2026, 2, 16), new DateTime(2026, 2, 17), new DateTime(2026, 2, 18) },
            "Tết Bính Ngọ"), CancellationToken.None);

        result.Added.Should().Be(3);
        result.SkippedWeekend.Should().Be(0);
        result.AlreadyExisted.Should().Be(0);
    }

    [Fact]
    public async Task Cuoi_tuan_bi_bo_qua_chu_khong_lam_vo_ca_lo()
    {
        _repo.Setup(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AddMarketClosuresCommandHandler(_repo.Object);
        var result = await handler.Handle(new AddMarketClosuresCommand("user1",
            new[]
            {
                new DateTime(2026, 4, 27),  // thứ Hai — hợp lệ
                new DateTime(2026, 8, 22),  // thứ Bảy — bỏ qua
                new DateTime(2026, 8, 23)   // Chủ nhật — bỏ qua
            }, null), CancellationToken.None);

        result.Added.Should().Be(1);
        result.SkippedWeekend.Should().Be(2);
        _repo.Verify(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Nhap_trung_thi_bao_da_ton_tai_chu_khong_nem()
    {
        _repo.Setup(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new AddMarketClosuresCommandHandler(_repo.Object);
        var result = await handler.Handle(new AddMarketClosuresCommand("user1",
            new[] { new DateTime(2026, 1, 1) }, null), CancellationToken.None);

        result.Added.Should().Be(0);
        result.AlreadyExisted.Should().Be(1);
    }

    [Fact]
    public async Task Ngay_trung_nhau_trong_cung_mot_lo_chi_ghi_mot_lan()
    {
        _repo.Setup(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AddMarketClosuresCommandHandler(_repo.Object);
        var result = await handler.Handle(new AddMarketClosuresCommand("user1",
            new[] { new DateTime(2026, 1, 1), new DateTime(2026, 1, 1) }, null), CancellationToken.None);

        result.Added.Should().Be(1);
        _repo.Verify(r => r.TryAddAsync(It.IsAny<MarketClosure>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Xoa_ngay_khong_ton_tai_thi_tra_false()
    {
        _repo.Setup(r => r.DeleteByDateAsync("user1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new RemoveMarketClosureCommandHandler(_repo.Object);
        var removed = await handler.Handle(
            new RemoveMarketClosureCommand("user1", new DateTime(2026, 7, 7)), CancellationToken.None);

        removed.Should().BeFalse();
    }

    [Fact]
    public async Task Doc_theo_nam_thi_nhom_theo_thang_va_ghi_chu_o_cap_ngay()
    {
        _repo.Setup(r => r.GetByUserAndRangeAsync("user1",
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MarketClosure("user1", new DateTime(2026, 4, 27), "Giỗ Tổ Hùng Vương"),
                new MarketClosure("user1", new DateTime(2026, 4, 30), "Ngày Chiến thắng"),
                new MarketClosure("user1", new DateTime(2026, 5, 1), "Quốc tế Lao động")
            });

        var handler = new GetMarketClosuresQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetMarketClosuresQuery("user1", 2026), CancellationToken.None);

        result.Year.Should().Be(2026);
        result.Months.Should().HaveCount(2);

        var april = result.Months.Single(m => m.Month == 4);
        april.Days.Should().HaveCount(2);
        // Tháng 4/2026 có HAI đợt lễ khác nhau — ghi chú phải ở cấp ngày, không phải cấp tháng.
        april.Days.Single(d => d.Day == 27).Note.Should().Be("Giỗ Tổ Hùng Vương");
        april.Days.Single(d => d.Day == 30).Note.Should().Be("Ngày Chiến thắng");
    }

    [Fact]
    public async Task Doc_theo_nam_thi_truyen_dung_bien_1_1_den_31_12()
    {
        DateTime from = default, to = default;
        _repo.Setup(r => r.GetByUserAndRangeAsync("user1",
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, DateTime, DateTime, CancellationToken>((_, f, t, _2) => (from, to) = (f, t))
            .ReturnsAsync(Array.Empty<MarketClosure>());

        var handler = new GetMarketClosuresQueryHandler(_repo.Object);
        await handler.Handle(new GetMarketClosuresQuery("user1", 2026), CancellationToken.None);

        from.Should().Be(new DateTime(2026, 1, 1));
        to.Should().Be(new DateTime(2026, 12, 31));
    }
}
