using FluentAssertions;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

/// <summary>
/// AI use-case mới <c>portfolio-critique</c> — dùng cho nút "🥊 AI phản biện danh mục"
/// trên Dashboard (P2 plan dashboard-decision-engine v1.1).
///
/// Vai trò AI: HLV phản biện adversarial (chỉ ra điểm SAI/YẾU/LỆCH KỶ LUẬT) — KHÔNG khen,
/// KHÔNG động viên. Replace use-case <c>daily-briefing</c> trên Home (vẫn giữ trong service
/// cho route khác).
///
/// Lock prompt content qua test — tránh AI prompt drift sang tone supportive theo update.
/// </summary>
public class AiAssistantServicePortfolioCritiqueTests
{
    [Fact]
    public void BuildPortfolioCritiqueSystemPrompt_RetainsBasePromptVietnameseRules()
    {
        var prompt = AiAssistantService.BuildPortfolioCritiqueSystemPrompt();

        prompt.Should().Contain("tiếng Việt", "phải giữ rule tiếng Việt từ BasePrompt");
        prompt.Should().Contain("markdown", "phải giữ markdown format từ BasePrompt");
    }

    [Fact]
    public void BuildPortfolioCritiqueSystemPrompt_FrameAsAdversarial_NotSupportive()
    {
        var prompt = AiAssistantService.BuildPortfolioCritiqueSystemPrompt();

        prompt.Should().Contain("phản biện", "vai trò AI là phản biện chứ không phải báo cáo");
        prompt.Should().Contain("KHÔNG khen", "AI không được khen — adversarial framing");
        prompt.Should().Contain("KHÔNG động viên", "AI không được dùng tone động viên");
    }

    [Fact]
    public void BuildPortfolioCritiqueSystemPrompt_RequiresExactlyThreePoints()
    {
        var prompt = AiAssistantService.BuildPortfolioCritiqueSystemPrompt();

        prompt.Should().Contain("3 điểm", "ép format output đúng 3 điểm phản biện");
        prompt.Should().Contain("1 câu", "mỗi điểm chỉ 1 câu — buộc đi thẳng vào vấn đề");
    }

    [Fact]
    public void BuildPortfolioCritiqueSystemPrompt_PrioritizesDisciplineViolations()
    {
        var prompt = AiAssistantService.BuildPortfolioCritiqueSystemPrompt();

        // Theo plan v1.1: ưu tiên SL violation > thesis expired > concentration > drawdown
        prompt.Should().Contain("SL", "stop-loss violation phải priority cao nhất");
        prompt.Should().Contain("thesis", "thesis expired là priority thứ 2");
    }

    [Fact]
    public void BuildPortfolioCritiqueSystemPrompt_UsesImperativeVerbs()
    {
        var prompt = AiAssistantService.BuildPortfolioCritiqueSystemPrompt();

        // Plan v1.1: "Dùng động từ mệnh lệnh: 'cắt', 'review', 'giảm'"
        // Tránh từ chung chung như "cân nhắc", "có thể"
        prompt.Should().Contain("mệnh lệnh", "ép AI dùng động từ mệnh lệnh");
        prompt.Should().NotContainEquivalentOf("hãy cân nhắc", "tone soft không cho phép");
    }

    [Fact]
    public void BuildPortfolioCritiqueSystemPrompt_DoesNotContainBriefingLanguage()
    {
        var prompt = AiAssistantService.BuildPortfolioCritiqueSystemPrompt();

        // Use-case mới khác hoàn toàn daily-briefing — không được nói "bản tin"
        prompt.Should().NotContainEquivalentOf("bản tin",
            "use-case này thay thế daily-briefing, không được nhầm role");
    }
}
