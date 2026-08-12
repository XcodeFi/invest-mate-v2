using FluentAssertions;
using InvestmentApp.Domain.Entities;
using Xunit;

namespace InvestmentApp.Domain.Tests.Entities;

public class MarketClosureTests
{
    [Fact]
    public void Date_duoc_chuan_hoa_ve_nua_dem_Utc()
    {
        var closure = new MarketClosure("user1", new DateTime(2026, 2, 17, 15, 30, 0), "Tết Bính Ngọ");

        closure.Date.Should().Be(new DateTime(2026, 2, 17, 0, 0, 0, DateTimeKind.Utc));
        closure.Date.Kind.Should().Be(DateTimeKind.Utc);
        closure.Note.Should().Be("Tết Bính Ngọ");
        closure.UserId.Should().Be("user1");
        closure.Id.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("2026-08-22")] // thứ Bảy
    [InlineData("2026-08-23")] // Chủ nhật
    public void Cuoi_tuan_bi_tu_choi_vi_da_la_ngay_nghi(string date)
    {
        var act = () => new MarketClosure("user1", DateTime.Parse(date));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Cuối tuần*");
    }

    [Fact]
    public void UserId_null_thi_nem()
    {
        var act = () => new MarketClosure(null!, new DateTime(2026, 1, 1));

        act.Should().Throw<ArgumentNullException>();
    }
}
