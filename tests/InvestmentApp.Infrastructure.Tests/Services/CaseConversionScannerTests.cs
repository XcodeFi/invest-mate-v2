using FluentAssertions;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Chính bộ quét của rào chắn "không còn .ToUpper()/.ToLower() theo văn hoá" cũng phải được test: bản quét từng dòng cắt ở "//" nên bỏ sót
/// lời gọi đứng sau một URL, và bỏ sót khi có khoảng trắng hoặc xuống dòng giữa tên hàm và dấu ngoặc.
/// </summary>
public class CaseConversionScannerTests
{
    private static List<int> Lines(string source) => CaseConversionScanner.FindCultureSensitiveCalls(source);

    [Theory]
    [InlineData("var u = symbol.ToUpper();")]
    [InlineData("var u = symbol.ToLower();")]
    [InlineData("var u = \"https://x.example/\" + symbol.ToUpper();")]            // "//" trong chuỗi trước lời gọi
    [InlineData("var u = $\"{\"http://a\"}/{symbol.ToLower()}\";")]                // lời gọi trong lỗ nội suy sau URL
    [InlineData("var u = $\"https://a.example/{symbol.ToLower()}/tin-tuc\";")]      // lời gọi trong lỗ nội suy
    [InlineData("var u = symbol.ToUpper ();")]                                      // khoảng trắng trước ngoặc
    [InlineData("var u = symbol . ToUpper ( ) ;")]                                  // khoảng trắng khắp nơi
    [InlineData("var u = symbol\n    .ToUpper\n    ();")]                           // tách dòng
    [InlineData("var q = '\"'; var u = symbol.ToUpper();")]                         // ký tự " không mở chuỗi
    [InlineData("var u = @\"C:\\a\" + symbol.ToUpper();")]                          // chuỗi nguyên văn kết thúc bằng \
    [InlineData("var u = \"a\\\"b\" + symbol.ToLower();")]                          // dấu " được escape trong chuỗi
    [InlineData("/* ghi chú */ var u = symbol.ToUpper();")]
    public void flags_the_culture_sensitive_call(string source)
    {
        Lines(source).Should().NotBeEmpty(source);
    }

    [Theory]
    [InlineData("// x.ToUpper()")]
    [InlineData("var a = 1; // x.ToLower()")]
    [InlineData("/* x.ToLower() */")]
    [InlineData("/* nhiều dòng\n x.ToUpper()\n */")]
    [InlineData("/// <summary>dùng x.ToUpper() ở đây</summary>")]
    [InlineData("var s = \"text .ToUpper() in a string\";")]
    [InlineData("var s = @\"verbatim .ToLower()\";")]
    [InlineData("var s = $\"literal .ToUpper() outside a hole {x}\";")]
    [InlineData("var s = $\"{x} then .ToUpper() is literal text\";")]               // chữ sau lỗ nội suy lại là chuỗi
    [InlineData("var s = $\"{Foo(new { A = 1 })} then .ToLower() is literal\";")]     // dấu {} lồng trong lỗ
    [InlineData("var s = $\"{{ .ToUpper() }}\";")]                                  // {{ là dấu ngoặc nhọn, không phải lỗ
    [InlineData("var u = symbol.ToUpperInvariant();")]
    [InlineData("var u = symbol.ToLowerInvariant();")]
    [InlineData("var u = symbol.ToUpper(CultureInfo.InvariantCulture);")]
    [InlineData("var u = symbol.ToLower(new CultureInfo(\"vi-VN\"));")]
    public void does_not_flag_comments_strings_or_the_invariant_forms(string source)
    {
        Lines(source).Should().BeEmpty(source);
    }

    [Fact]
    public void reports_the_one_based_line_of_the_call_even_when_the_call_is_split_across_lines()
    {
        var source = "var a = 1;\nvar b = \"x\";\nvar u = symbol\n    .ToUpper\n    ();\nvar c = 2;";

        Lines(source).Should().Equal(4);   // dòng chứa ".ToUpper"
    }

    [Fact]
    public void reports_every_occurrence()
    {
        var source = "a.ToUpper();\n// b.ToUpper();\nc.ToLower();\nvar s = \"https://x/\" + d.ToUpper();";

        Lines(source).Should().Equal(1, 3, 4);
    }

    [Fact]
    public void an_url_in_a_string_does_not_hide_a_later_call_on_the_same_line_but_a_real_comment_does()
    {
        Lines("var u = \"http://a\" + s.ToUpper(); // ok").Should().Equal(1);
        Lines("var u = \"http://a\"; // s.ToUpper()").Should().BeEmpty();
    }
}
