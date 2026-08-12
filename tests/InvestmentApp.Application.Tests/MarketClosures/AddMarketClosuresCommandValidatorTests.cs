using FluentAssertions;
using InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;

namespace InvestmentApp.Application.Tests.MarketClosures;

/// <summary>
/// `SuppressModelStateInvalidFilter = true` nên không có 400 tự động: body thiếu hẳn key
/// `dates` sẽ bind `Dates = null` và đi thẳng vào handler.
///
/// Không có validator thì vẫn ra 400 — `Enumerable.Select` trên null ném
/// `ArgumentNullException`, mà `ExceptionMiddleware` map `ArgumentException` sang 400.
/// Nhưng thân lỗi là "Value cannot be null. (Parameter 'source')", không nói gì về `dates`.
/// Validator đổi nó thành `errors` có cấu trúc, nói rõ phải gửi gì — đó là giá trị thật
/// của nó, KHÔNG phải chuyện cứu một lỗi 500.
/// </summary>
public class AddMarketClosuresCommandValidatorTests
{
    private readonly AddMarketClosuresCommandValidator _validator = new();

    [Fact]
    public void Thieu_han_key_dates_bi_chan_o_validator_chu_khong_vao_handler()
    {
        var result = _validator.Validate(new AddMarketClosuresCommand("user1", null!, "Tết"));

        result.IsValid.Should().BeFalse();
        // Lỗi phải nói phải gửi GÌ cho đúng, không chỉ nói là sai.
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("YYYY-MM-DD");
    }

    [Fact]
    public void Mang_dates_rong_cung_bi_chan()
    {
        var result = _validator.Validate(
            new AddMarketClosuresCommand("user1", Array.Empty<DateTime>(), null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Danh_sach_hop_le_thi_qua()
    {
        var result = _validator.Validate(new AddMarketClosuresCommand(
            "user1", new[] { new DateTime(2026, 4, 30) }, null));

        result.IsValid.Should().BeTrue(
            string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public async Task Handler_van_nem_khi_Dates_null_de_khong_ai_bo_validator_di()
    {
        // Ghim hành vi thật của handler khi không có validator chắn: `Select` trên null ném
        // ArgumentNullException (KHÔNG phải NullReferenceException — extension method không
        // deref cái null ở chỗ gọi). Không thêm guard vào handler: guard sẽ che việc
        // validator bị xoá, mà lại đổi 400 thành "0 đã thêm" im lặng.
        var handler = new AddMarketClosuresCommandHandler(
            new Moq.Mock<Application.Interfaces.IMarketClosureRepository>().Object);

        var act = async () => await handler.Handle(
            new AddMarketClosuresCommand("user1", null!, null), CancellationToken.None);

        // PHẢI await: ThrowAsync trả Task, bỏ await là assertion không bao giờ đỏ được.
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
