using System.Text;
using System.Text.RegularExpressions;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// Tìm lời gọi <c>.ToUpper()</c> / <c>.ToLower()</c> KHÔNG tham số (chạy theo văn hoá của máy) trong mã C#. Quét cả file, không theo dòng:
/// bỏ chú thích và nội dung chuỗi (kể cả chuỗi nguyên văn và chuỗi nội suy, nhưng GIỮ phần mã trong lỗ nội suy <c>{...}</c>), rồi so khớp
/// <c>.\s*(ToUpper|ToLower)\s*\(\s*\)</c>, nên bắt được cả khi có khoảng trắng hoặc xuống dòng, và khi đứng sau một URL chứa "//".
/// </summary>
internal static class CaseConversionScanner
{
    private static readonly Regex Call = new(@"\.\s*(ToUpper|ToLower)\s*\(\s*\)", RegexOptions.Compiled);

    /// <summary>Số dòng (từ 1) của từng lời gọi tìm thấy; với lời gọi tách dòng thì là dòng của ".ToUpper".</summary>
    public static List<int> FindCultureSensitiveCalls(string source)
    {
        var code = StripNonCode(source);
        var lines = new List<int>();
        foreach (Match m in Call.Matches(code))
        {
            var dot = m.Index;
            var line = 1;
            for (var k = 0; k < dot; k++) if (code[k] == '\n') line++;
            // lời gọi tách dòng: lấy dòng chứa tên hàm, không phải dòng chứa dấu chấm
            var nameAt = m.Groups[1].Index;
            line = 1;
            for (var k = 0; k < nameAt; k++) if (code[k] == '\n') line++;
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Giữ nguyên độ dài và các dấu xuống dòng; chú thích và nội dung chuỗi thành khoảng trắng.</summary>
    internal static string StripNonCode(string s)
    {
        var sb = new StringBuilder(s.Length);
        var i = 0;
        ScanCode(s, ref i, sb, inHole: false);
        return sb.ToString();
    }

    private static void Blank(StringBuilder sb, char c) => sb.Append(c == '\n' || c == '\r' ? c : ' ');

    private static void ScanCode(string s, ref int i, StringBuilder sb, bool inHole)
    {
        var depth = 0;
        while (i < s.Length)
        {
            var c = s[i];

            if (inHole)
            {
                if (c == '{') depth++;
                else if (c == '}')
                {
                    if (depth == 0) return;   // hết lỗ nội suy; người gọi tiêu thụ dấu }
                    depth--;
                }
            }

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') { Blank(sb, s[i]); i++; }
                continue;
            }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                Blank(sb, s[i++]); Blank(sb, s[i++]);
                while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/')) { Blank(sb, s[i]); i++; }
                if (i < s.Length) { Blank(sb, s[i++]); }
                if (i < s.Length) { Blank(sb, s[i++]); }
                continue;
            }
            if (c == '\'')
            {
                // ký tự: '"' hay '\'' không được mở/đóng chuỗi
                Blank(sb, s[i++]);
                while (i < s.Length && s[i] != '\'')
                {
                    if (s[i] == '\\' && i + 1 < s.Length) { Blank(sb, s[i++]); }
                    Blank(sb, s[i++]);
                }
                if (i < s.Length) Blank(sb, s[i++]);
                continue;
            }
            if (TryStringStart(s, i, out var dollars, out var verbatim, out var rawQuotes, out var prefixLen))
            {
                for (var k = 0; k < prefixLen; k++) Blank(sb, s[i + k]);
                i += prefixLen;
                ScanString(s, ref i, sb, dollars, verbatim, rawQuotes);
                continue;
            }

            sb.Append(c);
            i++;
        }
    }

    /// <summary>Nhận diện đầu chuỗi: <c>"</c>, <c>@"</c>, <c>$"</c>, <c>$@"</c>, <c>@$"</c>, và chuỗi thô <c>"""</c>. prefixLen gồm cả dấu " mở.</summary>
    private static bool TryStringStart(string s, int i, out int dollars, out bool verbatim, out int rawQuotes, out int prefixLen)
    {
        dollars = 0;
        verbatim = false;
        rawQuotes = 0;
        prefixLen = 0;
        var j = i;
        // tiền tố: một hoặc nhiều '$' (chuỗi thô có thể $$"""), và tối đa một '@' (chỉ chuỗi nguyên văn thường)
        while (j < s.Length && (s[j] == '$' || (s[j] == '@' && !verbatim)))
        {
            if (s[j] == '$') dollars++; else verbatim = true;
            j++;
        }
        if (j >= s.Length || s[j] != '"') { dollars = 0; verbatim = false; return false; }

        // Chuỗi thô: một chuỗi từ 3 dấu " liền nhau trở lên (sau tiền tố $), không đi với @. Số dấu " mở = độ dài dấu đóng.
        var q = 0;
        while (j + q < s.Length && s[j + q] == '"') q++;
        if (q >= 3 && !verbatim)
        {
            rawQuotes = q;
            prefixLen = (j - i) + q;
            return true;
        }

        prefixLen = j - i + 1;
        return true;
    }

    private static void ScanString(string s, ref int i, StringBuilder sb, int dollars, bool verbatim, int rawQuotes)
    {
        var interpolated = dollars > 0;
        if (rawQuotes > 0)
        {
            ScanRawString(s, ref i, sb, dollars, rawQuotes);
            return;
        }

        while (i < s.Length)
        {
            var c = s[i];
            if (verbatim)
            {
                if (c == '"')
                {
                    if (i + 1 < s.Length && s[i + 1] == '"') { Blank(sb, s[i++]); Blank(sb, s[i++]); continue; }
                    Blank(sb, s[i++]);
                    return;
                }
            }
            else
            {
                if (c == '\\' && i + 1 < s.Length) { Blank(sb, s[i++]); Blank(sb, s[i++]); continue; }
                if (c == '"') { Blank(sb, s[i++]); return; }
            }

            if (interpolated && c == '{')
            {
                if (i + 1 < s.Length && s[i + 1] == '{') { Blank(sb, s[i++]); Blank(sb, s[i++]); continue; }   // {{ là dấu { chữ
                Blank(sb, s[i++]);                  // dấu { mở lỗ
                ScanCode(s, ref i, sb, inHole: true);
                if (i < s.Length) Blank(sb, s[i++]); // dấu } đóng lỗ
                continue;
            }
            if (interpolated && c == '}' && i + 1 < s.Length && s[i + 1] == '}') { Blank(sb, s[i++]); Blank(sb, s[i++]); continue; }

            Blank(sb, c);
            i++;
        }
    }

    /// <summary>
    /// Chuỗi thô: kết thúc bằng một dãy dấu " dài ít nhất bằng dãy mở. Với <paramref name="dollars"/> = k dấu '$', một lỗ nội suy mở bằng đúng k dấu '{'
    /// (dãy '{' dài hơn k thì phần dư là chữ) và đóng bằng k dấu '}'; dãy '{' ngắn hơn k là chữ.
    /// </summary>
    private static void ScanRawString(string s, ref int i, StringBuilder sb, int dollars, int openQuotes)
    {
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '"')
            {
                var run = 0;
                while (i + run < s.Length && s[i + run] == '"') run++;
                for (var k = 0; k < run; k++) Blank(sb, s[i + k]);
                i += run;
                if (run >= openQuotes) return;
                continue;
            }
            if (dollars > 0 && c == '{')
            {
                var run = 0;
                while (i + run < s.Length && s[i + run] == '{') run++;
                if (run < dollars)
                {
                    for (var k = 0; k < run; k++) Blank(sb, s[i + k]);
                    i += run;
                    continue;
                }
                for (var k = 0; k < run; k++) Blank(sb, s[i + k]);   // dấu { dư là chữ, k dấu cuối mở lỗ
                i += run;
                ScanCode(s, ref i, sb, inHole: true);
                var closing = 0;
                while (closing < dollars && i < s.Length && s[i] == '}') { Blank(sb, s[i++]); closing++; }
                continue;
            }
            Blank(sb, c);
            i++;
        }
    }
}
