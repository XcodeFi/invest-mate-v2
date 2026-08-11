using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.Mood.Commands.MarkMoodOverride;

/// <summary>
/// Đóng dấu người dùng đã bấm qua lớp phủ Hàng đợi quyết định. Trả false khi hôm nay chưa
/// chấm tâm trạng — không đẻ bản ghi ma cho một trạng thái chưa ai khai.
/// </summary>
public class MarkMoodOverrideCommand : IRequest<bool>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class MarkMoodOverrideCommandHandler : IRequestHandler<MarkMoodOverrideCommand, bool>
{
    private readonly IMoodCheckInRepository _repo;

    public MarkMoodOverrideCommandHandler(IMoodCheckInRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(MarkMoodOverrideCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dateKey = VietnamDate.ToDateKey(now);

        var existing = await _repo.GetByUserAndDateAsync(request.UserId, dateKey, cancellationToken);
        if (existing is null) return false;

        existing.MarkOverridden(now);
        await _repo.UpdateAsync(existing, cancellationToken);
        return true;
    }
}
