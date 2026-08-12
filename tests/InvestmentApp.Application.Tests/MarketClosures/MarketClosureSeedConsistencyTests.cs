using FluentAssertions;

namespace InvestmentApp.Application.Tests.MarketClosures;

/// <summary>
/// Ghim danh sách ngày nghỉ 2026 mà các ca golden T+2 dựa vào. Sửa script seed mà không
/// sửa đây (hoặc ngược lại) thì test golden Tết vẫn xanh trong khi dữ liệu thật đã lệch.
/// Nguồn: thông báo lịch nghỉ giao dịch năm 2026 của HOSE — 12 phiên.
/// </summary>
public static class Vn2026Closures
{
    public static readonly string[] Dates =
    {
        "2026-01-01",
        "2026-02-16", "2026-02-17", "2026-02-18", "2026-02-19", "2026-02-20",
        "2026-04-27",
        "2026-04-30", "2026-05-01",
        "2026-08-31", "2026-09-01", "2026-09-02"
    };
}

public class MarketClosureSeedConsistencyTests
{
    [Fact]
    public void Seed_2026_co_dung_12_phien_nghi()
    {
        Vn2026Closures.Dates.Should().HaveCount(12);
        Vn2026Closures.Dates.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Khong_ngay_nao_trong_seed_la_cuoi_tuan()
    {
        foreach (var date in Vn2026Closures.Dates)
        {
            var parsed = DateTime.Parse(date);
            parsed.DayOfWeek.Should().NotBe(DayOfWeek.Saturday, $"{date} là thứ Bảy, không cần lưu");
            parsed.DayOfWeek.Should().NotBe(DayOfWeek.Sunday, $"{date} là Chủ nhật, không cần lưu");
        }
    }

    [Fact]
    public void Script_seed_chua_dung_12_ngay_nhu_hang_so()
    {
        var script = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "migrations",
            "2026-08-12-market-closures-2026.mongo.js"));

        foreach (var date in Vn2026Closures.Dates)
            script.Should().Contain($"\"{date}\"", $"script seed phải chứa {date}");
    }

    [Fact]
    public void Script_seed_khong_chua_ngay_la_nao_ngoai_danh_sach()
    {
        var script = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "migrations",
            "2026-08-12-market-closures-2026.mongo.js"));

        // Bắt cả chiều ngược lại: thêm ngày vào script mà quên cập nhật hằng số thì
        // ca golden vẫn chạy trên tập cũ và không ai biết.
        // Chỉ quét trong khối CLOSURES — dòng verify/rollback ở cuối script cũng chứa
        // ngày 2026 (biên truy vấn), quét cả file là bắt sai.
        var block = System.Text.RegularExpressions.Regex.Match(
            script, @"const CLOSURES = \[(?<body>.*?)\];",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        block.Success.Should().BeTrue("không tìm thấy khối `const CLOSURES = [...]` trong script seed");

        var inScript = System.Text.RegularExpressions.Regex
            .Matches(block.Groups["body"].Value, @"""(\d{4}-\d{2}-\d{2})""")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();

        inScript.Should().BeEquivalentTo(Vn2026Closures.Dates);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy gốc repo");
    }
}
