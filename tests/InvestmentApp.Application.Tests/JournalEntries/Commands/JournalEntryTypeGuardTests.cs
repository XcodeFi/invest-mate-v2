using FluentAssertions;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Application.JournalEntries.Commands.UpdateJournalEntry;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.JournalEntries.Commands;

/// <summary>
/// <c>JournalEntryType.Decision</c> là cờ dập cảnh báo: <c>GetDecisionQueueQuery</c> gom mọi mục
/// nhật ký loại này trong ngày VN rồi lọc thẻ tương ứng ra khỏi Hàng đợi quyết định. Nó chỉ được
/// phép sinh từ đường resolve (nơi có luật "GIỮ phải ghi lý do ≥ 20 ký tự"), không phải từ đường
/// tạo nhật ký thường.
///
/// Trước khi có guard này, tập giá trị hợp lệ chỉ tồn tại trong <c>[Description]</c> của tool MCP
/// và một comment cạnh property — còn handler thì <c>Enum.Parse</c> thẳng, nên
/// <c>entryType: "Decision"</c> đi qua và dập được cảnh báo mà không cần lý do nào.
/// </summary>
public class JournalEntryTypeGuardTests
{
    private static CreateJournalEntryCommand MakeCreate(string entryType) => new()
    {
        UserId = "user-1",
        Symbol = "FPT",
        EntryType = entryType,
        Title = "Ghi nhận",
        Content = "Nội dung ghi chú đủ dài để không vướng luật khác"
    };

    private static readonly string[] AllowedTypes =
    {
        nameof(JournalEntryType.Observation),
        nameof(JournalEntryType.PreTrade),
        nameof(JournalEntryType.DuringTrade),
        nameof(JournalEntryType.PostTrade),
        nameof(JournalEntryType.Review)
    };

    [Fact]
    public void Create_DecisionType_IsRejected()
    {
        var result = new CreateJournalEntryCommandValidator().Validate(MakeCreate("Decision"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateJournalEntryCommand.EntryType));
    }

    [Theory]
    [InlineData("decision")]
    [InlineData("DECISION")]
    [InlineData("  Decision  ")]
    [InlineData("5")]
    public void Create_DecisionType_IsRejected_RegardlessOfCaseOrPadding(string entryType)
    {
        // Handler dùng Enum.Parse(ignoreCase: true) nên mọi biến thể chữ hoa/thường đều parse
        // thành Decision — và Enum.Parse còn nhận cả CHUỖI SỐ, nên "5" cũng ra Decision.
        // Guard phải chặn đúng tập đó, không chỉ chuỗi viết hoa chuẩn.
        new CreateJournalEntryCommandValidator().Validate(MakeCreate(entryType))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_AllOtherEnumValues_AreAccepted()
    {
        // Ca đối chứng: guard không được chặn lây. Quét theo enum thật nên thêm loại mới
        // vào JournalEntryType mà quên khai vào allowlist thì test này đỏ.
        foreach (var t in AllowedTypes)
            new CreateJournalEntryCommandValidator().Validate(MakeCreate(t))
                .IsValid.Should().BeTrue($"{t} phải được phép");
    }

    [Fact]
    public void Create_UnknownType_IsRejected()
    {
        // Trước đây chuỗi lạ làm Enum.Parse throw ArgumentException → 400 nhưng message vô nghĩa
        // với agent. Chặn ở validator cho ra danh sách giá trị hợp lệ.
        new CreateJournalEntryCommandValidator().Validate(MakeCreate("Whatever"))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_DecisionType_IsRejected()
    {
        var cmd = new UpdateJournalEntryCommand { Id = "j1", UserId = "user-1", EntryType = "Decision" };

        new UpdateJournalEntryCommandValidator().Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_NullEntryType_IsAccepted()
    {
        // EntryType null = giữ nguyên loại cũ. Chặn null là chặn mọi lần sửa tiêu đề/nội dung.
        var cmd = new UpdateJournalEntryCommand { Id = "j1", UserId = "user-1", EntryType = null };

        new UpdateJournalEntryCommandValidator().Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_OtherEnumValues_AreAccepted()
    {
        foreach (var t in AllowedTypes)
        {
            var cmd = new UpdateJournalEntryCommand { Id = "j1", UserId = "user-1", EntryType = t };
            new UpdateJournalEntryCommandValidator().Validate(cmd)
                .IsValid.Should().BeTrue($"{t} phải được phép");
        }
    }

    [Fact]
    public void AllowedTypes_CoversEveryEnumValueExceptDecision()
    {
        // Ghim mối quan hệ giữa allowlist và enum. Thêm giá trị mới vào JournalEntryType mà không
        // quyết định nó thuộc phía nào thì test này đỏ ngay, thay vì lặng lẽ bị chặn.
        var all = Enum.GetNames<JournalEntryType>();

        all.Should().BeEquivalentTo(AllowedTypes.Append(nameof(JournalEntryType.Decision)));
    }
}
