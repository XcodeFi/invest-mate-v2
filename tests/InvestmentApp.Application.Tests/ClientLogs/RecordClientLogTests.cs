using FluentAssertions;
using InvestmentApp.Application.ClientLogs.Commands.RecordClientLog;
using Microsoft.Extensions.Logging;
using Moq;

namespace InvestmentApp.Application.Tests.ClientLogs;

/// <summary>
/// Relay cho lỗi phía trình duyệt. Frontend KHÔNG gọi Telegram trực tiếp — bot token sẽ nằm
/// trong bundle JS mà ai cũng đọc được — nên nó đẩy về đây, và Serilog mới chuyển tiếp đi.
/// </summary>
public class RecordClientLogCommandValidatorTests
{
    private readonly RecordClientLogCommandValidator _validator = new();

    private static RecordClientLogCommand Valid() => new()
    {
        UserId = "user-1",
        Message = "TypeError: cannot read property 'foo' of undefined",
        Url = "/company-dossier/HAH",
        Timestamp = "2026-08-11T09:00:00Z",
    };

    [Fact]
    public void Message_rong_thi_khong_hop_le()
    {
        var cmd = Valid();
        cmd.Message = "   ";

        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("password")]
    [InlineData("Bearer token")]
    [InlineData("apiKey")]
    [InlineData("client_secret")]
    [InlineData("EMAIL")]
    public void Message_chua_ten_truong_nhay_cam_thi_bi_chan(string needle)
    {
        // Không phải để bắt kẻ tấn công — người dùng tự gửi lỗi của chính mình. Đây là chốt cuối
        // chặn ca frontend lỡ dump nguyên header hoặc object cấu hình vào message.
        var cmd = Valid();
        cmd.Message = $"Lỗi khi xử lý {needle} trong request";

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("không được phép"));
    }

    [Fact]
    public void Url_chua_email_thi_bi_chan()
    {
        var cmd = Valid();
        cmd.Url = "/reset?email=nguoidung@gmail.com";

        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Payload_hop_le_thi_qua()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Message_qua_dai_thi_khong_hop_le()
    {
        var cmd = Valid();
        cmd.Message = new string('x', 501);

        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }
}

public class RecordClientLogCommandHandlerTests
{
    private readonly Mock<ILogger<RecordClientLogCommandHandler>> _logger = new();

    private RecordClientLogCommandHandler Sut() => new(_logger.Object);

    private static RecordClientLogCommand Valid() => new()
    {
        UserId = "user-1",
        Message = "TypeError",
        Url = "/dashboard",
        Timestamp = "2026-08-11T09:00:00Z",
    };

    /// <summary>Bắt lại đúng chuỗi đã được đưa vào ILogger để soi độ dài thật, không đoán.</summary>
    private string CapturedLogText()
    {
        string? captured = null;
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => Capture(v.ToString(), ref captured)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        return captured ?? "";
    }

    private static bool Capture(string? value, ref string? slot)
    {
        slot = value;
        return true;
    }

    [Fact]
    public async Task Stack_dai_bi_cat_con_dung_1000_ky_tu_truoc_khi_log()
    {
        // Khẳng định ĐỘ DÀI thật sự đi vào log, không phải chỉ khẳng định "không ném".
        // Telegram giới hạn 4096 ký tự cho cả tin nhắn; một stack 20k sẽ nuốt hết chỗ của
        // mọi thông tin còn lại rồi bị nhà cung cấp cắt ở chỗ không ai chọn.
        var cmd = Valid();
        cmd.Stack = new string('S', 20_000);

        await Sut().Handle(cmd, default);

        var logged = CapturedLogText();
        var run = new string('S', 1000);
        logged.Should().Contain(run);
        logged.Should().NotContain(run + "S");
    }

    [Fact]
    public async Task Stack_null_van_log_duoc()
    {
        var cmd = Valid();
        cmd.Stack = null;

        var act = async () => await Sut().Handle(cmd, default);

        await act.Should().NotThrowAsync();
        CapturedLogText().Should().Contain("TypeError");
    }

    [Fact]
    public async Task Ghi_dung_mot_dong_muc_Error_kem_userId_va_url()
    {
        await Sut().Handle(Valid(), default);

        var logged = CapturedLogText();
        logged.Should().Contain("user-1");
        logged.Should().Contain("/dashboard");
    }
}
