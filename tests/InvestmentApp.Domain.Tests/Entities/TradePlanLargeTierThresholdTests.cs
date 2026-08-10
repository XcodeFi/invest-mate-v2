using System.Runtime.CompilerServices;
using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

/// <summary>
/// TradePlan.LargeTierThreshold từng LÀ 4 literal 0.05m độc lập (TradePlan.EnsureDisciplineGate,
/// CompanyDossierGate, UpdateTradePlanCommand, DisciplineScoreCalculator) trùng nhau chỉ do trùng
/// lặp trong ~4 tháng, không do cơ chế nào ép — không ai báo khi một chỗ đổi mà 3 chỗ còn lại
/// không đổi theo. Test này (a) ghim giá trị hằng số, (b) quét cả 4 file tiêu thụ để đảm bảo
/// không ai lặng lẽ quay lại literal 0.05m thay vì tham chiếu hằng số. Nhìn tách biệt, một test
/// ghim giá trị có vẻ vô nghĩa — lý do tồn tại nằm ở phần (b).
/// </summary>
public class TradePlanLargeTierThresholdTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private const string DeclarationFile = "src/InvestmentApp.Domain/Entities/TradePlan.cs";
    private const string DossierGateFile = "src/InvestmentApp.Application/CompanyDossiers/Gate/CompanyDossierGate.cs";
    private const string UpdateCommandFile = "src/InvestmentApp.Application/TradePlans/Commands/UpdateTradePlan/UpdateTradePlanCommand.cs";
    private const string DisciplineScoreFile = "src/InvestmentApp.Infrastructure/Services/DisciplineScoreCalculator.cs";

    public static IEnumerable<object[]> AllConsumerFiles()
    {
        yield return new object[] { DeclarationFile };
        yield return new object[] { DossierGateFile };
        yield return new object[] { UpdateCommandFile };
        yield return new object[] { DisciplineScoreFile };
    }

    // File khai báo được đúng 1 dòng-code chứa "0.05m" (chính dòng khai báo); 3 file tiêu thụ
    // còn lại phải là 0 — nếu khác thì có ai đã tự chép công thức 0.05m thay vì tham chiếu.
    public static IEnumerable<object[]> MaxBareLiteralLinesPerFile()
    {
        yield return new object[] { DeclarationFile, 1 };
        yield return new object[] { DossierGateFile, 0 };
        yield return new object[] { UpdateCommandFile, 0 };
        yield return new object[] { DisciplineScoreFile, 0 };
    }

    [Fact]
    public void LargeTierThreshold_IsPinnedAtFivePercent()
    {
        TradePlan.LargeTierThreshold.Should().Be(0.05m);
    }

    [Theory]
    [MemberData(nameof(AllConsumerFiles))]
    public void EveryConsumer_StillReferencesSharedConstant(string relativePath)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, relativePath));
        text.Should().Contain("LargeTierThreshold",
            $"{relativePath} phải tham chiếu TradePlan.LargeTierThreshold — nếu không thì không còn là consumer của hằng số chung nữa");
    }

    [Theory]
    [MemberData(nameof(MaxBareLiteralLinesPerFile))]
    public void NoConsumer_ReintroducesBareLiteral(string relativePath, int maxBareLiteralLines)
    {
        var codeLines = File.ReadAllLines(Path.Combine(RepoRoot, relativePath))
            .Where(l => !l.TrimStart().StartsWith("//")); // bỏ dòng comment (kể cả /// doc comment)

        var bareLiteralLineCount = codeLines.Count(l => l.Contains("0.05m"));

        bareLiteralLineCount.Should().Be(maxBareLiteralLines,
            $"{relativePath} có literal 0.05m thô ngoài dòng khai báo hằng số — phải tham chiếu " +
            "TradePlan.LargeTierThreshold, nếu không 4 chỗ lại trùng nhau chỉ do trùng lặp như trước");
    }

    private static string FindRepoRoot([CallerFilePath] string thisFile = "")
    {
        var dir = Path.GetDirectoryName(thisFile)!;
        while (!File.Exists(Path.Combine(dir, "InvestmentApp.sln")))
            dir = Path.GetDirectoryName(dir)
                ?? throw new InvalidOperationException($"Không tìm được InvestmentApp.sln từ {thisFile}");
        return dir;
    }
}
