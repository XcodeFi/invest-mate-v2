using System.ComponentModel;
using InvestmentApp.Application.Mood.Commands.SetMood;
using InvestmentApp.Application.Mood.Queries.GetMoodHistory;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace InvestmentApp.Api.Mcp;

/// <summary>
/// Tâm trạng tự chấm mỗi ngày (ADR-0013). Agent ĐỌC lịch sử để hiểu người dùng, và chỉ GHI lại điều chính người dùng nói về bản thân.
/// Ghi từ MCP mang nguồn "mcp": không bao giờ đặt hay xoá dấu "đã bấm qua lớp phủ", và "Bình tĩnh" do agent ghi không mở khoá lớp phủ trang chủ.
/// </summary>
[McpServerToolType]
public static class MoodTools
{
    [McpServerTool(Name = "get_mood_history", ReadOnly = true)]
    [Description("Lịch sử tâm trạng tự chấm theo ngày (giờ Việt Nam): mỗi ngày có tâm trạng, các lần đổi trong ngày (kèm nguồn web/mcp), đã bấm qua lớp phủ chưa, số lệnh trong ngày và emotionalTrade (đặt lệnh khi đang cảm xúc: đã bấm qua lớp phủ, hoặc có lệnh trong ngày mà tâm trạng ở bất kỳ lần đổi nào khác Bình tĩnh). Ngày có lệnh nhưng chưa chấm có mood null (\"chưa chấm\", không bao giờ tính là cảm xúc). Chỉ có ngày đã chấm hoặc có lệnh. Mặc định 90 ngày gần nhất, tối đa 366 ngày.")]
    public static async Task<MoodHistoryDto> GetMoodHistory(
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct,
        // from/to phải nằm SAU ct và có `= null`: nullable một mình không đủ để schema đánh dấu optional.
        [Description("Từ ngày (giờ VN, YYYY-MM-DD). Bỏ trống: 90 ngày kết thúc ở 'to'.")] DateTime? from = null,
        [Description("Đến ngày (giờ VN, gồm cả ngày này). Bỏ trống: hôm nay.")] DateTime? to = null)
        => await mediator.Send(new GetMoodHistoryQuery { UserId = http.GetUserId(), From = from, To = to }, ct);

    [McpServerTool(Name = "set_today_mood", Destructive = false)]
    [Description("Ghi tâm trạng HÔM NAY (giờ VN) theo đúng điều NGƯỜI DÙNG vừa nói về chính họ. Tuyệt đối không tự đoán hay suy ra từ hành vi của họ; không chắc thì hỏi lại, đừng ghi. mood: Calm (bình tĩnh), Fomo, Fear (sợ), Revenge (gỡ). Ghi lại đúng trạng thái hiện tại là no-op. Lưu ý: 'Calm' do agent ghi KHÔNG mở khoá lớp phủ trên trang chủ (người dùng vẫn được hỏi lại và thấy ghi chú '(từ agent)'); trạng thái cảm xúc do agent ghi thì có hiệu lực. Công cụ này không bao giờ thay đổi dấu 'đã bấm qua lớp phủ'.")]
    public static async Task<string> SetTodayMood(
        [Description("Calm | Fomo | Fear | Revenge (không phân biệt hoa thường).")] string mood,
        IMediator mediator, IHttpContextAccessor http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mood) || !Enum.TryParse<MoodState>(mood.Trim(), ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed) || int.TryParse(mood.Trim(), out _))
            throw new ArgumentException("Tâm trạng không hợp lệ. Dùng một trong: Calm (bình tĩnh), Fomo, Fear (sợ), Revenge (gỡ).");

        await mediator.Send(new SetMoodCommand { UserId = http.GetUserId(), Mood = parsed, Source = MoodChange.SourceMcp }, ct);
        return $"Đã ghi tâm trạng hôm nay: {parsed} (nguồn: agent).";
    }
}
