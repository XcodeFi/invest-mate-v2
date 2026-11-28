using FluentAssertions;

namespace InvestmentApp.Infrastructure.Tests.Backtest;

/// <summary>Phần dựng kết nối của lần chạy thật: bắt buộc khai báo tường minh (không có đường dự phòng tới cấu hình dev trỏ vào cơ sở dữ liệu prod) và không rò thông tin kết nối qua thông báo lỗi.</summary>
public class PatternBacktestLiveSetupTests
{
    private static Func<string, string?> Env(params (string k, string v)[] pairs) => k => pairs.FirstOrDefault(p => p.k == k).v;

    [Fact]
    public void the_connection_and_the_database_must_both_be_given_explicitly_there_is_no_fallback_to_a_config_file()
    {
        var none = () => PatternBacktestLive.ResolveConnection(Env());
        var onlyConn = () => PatternBacktestLive.ResolveConnection(Env(("PATTERN_BACKTEST_MONGO_CONN", "mongodb://x")));
        var onlyDb = () => PatternBacktestLive.ResolveConnection(Env(("PATTERN_BACKTEST_MONGO_DB", "InvestmentApp")));

        none.Should().Throw<InvalidOperationException>().WithMessage("*PATTERN_BACKTEST_MONGO_CONN*");
        onlyConn.Should().Throw<InvalidOperationException>().WithMessage("*PATTERN_BACKTEST_MONGO_DB*");
        onlyDb.Should().Throw<InvalidOperationException>().WithMessage("*PATTERN_BACKTEST_MONGO_CONN*");
    }

    [Fact]
    public void both_given_resolves_to_exactly_those_values()
    {
        var (c, d) = PatternBacktestLive.ResolveConnection(Env(("PATTERN_BACKTEST_MONGO_CONN", "mongodb://x"), ("PATTERN_BACKTEST_MONGO_DB", "InvestmentApp")));

        (c, d).Should().Be(("mongodb://x", "InvestmentApp"));
    }

    [Fact]
    public void a_malformed_connection_string_fails_with_the_exception_type_only_never_the_string()
    {
        const string secret = "tên-máy-chủ-bí-mật:hunter2@";
        var act = () => PatternBacktestLive.OpenReadOnly("not a connection string " + secret, "db");

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().NotContain("hunter2").And.NotContain("bí-mật").And.NotContain("not a connection string");
        ex.Message.Should().Contain("Exception", "thông báo chỉ nêu tên kiểu ngoại lệ của driver");
        ex.InnerException.Should().BeNull("không giữ ngoại lệ gốc: nó mang thông báo của driver");
    }

    [Fact]
    public void the_live_run_is_reported_as_skipped_not_passed_unless_explicitly_enabled()
    {
        if (Environment.GetEnvironmentVariable("PATTERN_BACKTEST") == "1") return;   // người dùng đã bật: không có gì để kiểm

        new LiveBacktestFactAttribute().Skip.Should().NotBeNullOrWhiteSpace().And.Contain("PATTERN_BACKTEST=1");
    }
}
