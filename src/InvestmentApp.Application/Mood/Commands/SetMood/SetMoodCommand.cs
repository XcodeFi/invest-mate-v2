using System.Text.Json.Serialization;
using InvestmentApp.Application.Common;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Mood.Commands.SetMood;

/// <summary>
/// Ghi tâm trạng cho ngày hôm nay. Ngày do server tính theo lịch VN — client không gửi ngày lên,
/// vì máy đặt sai giờ hoặc người dùng sửa tay là hỏng.
/// </summary>
public class SetMoodCommand : IRequest<Unit>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;

    public MoodState Mood { get; set; }
}

public class SetMoodCommandHandler : IRequestHandler<SetMoodCommand, Unit>
{
    private readonly IMoodCheckInRepository _repo;

    public SetMoodCommandHandler(IMoodCheckInRepository repo)
    {
        _repo = repo;
    }

    public async Task<Unit> Handle(SetMoodCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dateKey = VietnamDate.ToDateKey(now);

        var existing = await _repo.GetByUserAndDateAsync(request.UserId, dateKey, cancellationToken);
        if (existing is null)
        {
            try
            {
                var created = MoodCheckIn.Create(request.UserId, dateKey, request.Mood, now);
                await _repo.AddAsync(created, cancellationToken);
                return Unit.Value;
            }
            catch (DuplicateMoodCheckInException)
            {
                // Bấm hai lần: request kia đã tạo xong giữa lúc ta tra và lúc ta ghi.
                // Tìm lại đúng một lần rồi rơi xuống đường cập nhật thường ngày — không retry vô hạn.
                existing = await _repo.GetByUserAndDateAsync(request.UserId, dateKey, cancellationToken);
                if (existing is null) throw;
            }
        }

        existing.SetMood(request.Mood, now);
        await _repo.UpdateAsync(existing, cancellationToken);
        return Unit.Value;
    }
}
