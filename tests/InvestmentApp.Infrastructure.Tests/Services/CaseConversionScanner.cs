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
            if (TryStringStart(s, i, out var interpolated, out var verbatim, out var raw, out var prefixLen))
            {
                for (var k = 0; k < prefixLen; k++) Blank(sb, s[i + k]);
                i += prefixLen;
                ScanString(s, ref i, sb, interpolated, verbatim, raw);
                continue;
            }

            sb.Append(c);
            i++;
        }
    }

    /// <summary>Nhận diện đầu chuỗi: <c>"</c>, <c>@"</c>, <c>$"</c>, <c>$@"</c>, <c>@$"</c>, và chuỗi thô <c>"""</c>. prefixLen gồm cả dấu " mở.</summary>
    private static bool TryStringStart(string s, int i, out bool interpolated, out bool verbatim, out bool raw, out int prefixLen)
    {
        interpolated = verbatim = raw = false;
        prefixLen = 0;
        var j = i;
        while (j < s.Length && (s[j] == '$' || s[j] == '@') && j - i < 2)
        {
            if (s[j] == '$') interpolated = true; else verbatim = true;
            j++;
        }
        if (j >= s.Length || s[j] != '"') { interpolated = verbatim = false; return false; }
        if (j == i && j + 2 < s.Length && s[j + 1] == '"' && s[j + 2] == '"') { raw = true; prefixLen = 3; return true; }
        prefixLen = j - i + 1;
        return true;
    }

    private static void ScanString(string s, ref int i, StringBuilder sb, bool interpolated, bool verbatim, bool raw)
    {
        while (i < s.Length)
        {
            var c = s[i];
            if (raw)
            {
                if (c == '"' && i + 2 < s.Length + 0 && s.AsSpan(i).StartsWith("\"\"\"")) { Blank(sb, s[i++]); Blank(sb, s[i++]); Blank(sb, s[i++]); return; }
                Blank(sb, c); i++;
                continue;
            }
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
}
