using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.MarketClosures.Commands.AddMarketClosures;

/// <summary>
/// Nhập ngày nghỉ giao dịch. Nhận một ngày, một đợt lễ, hay cả năm — cùng một đường.
/// Idempotent: gửi lại danh sách đã nhập thì không đẻ bản ghi thứ hai.
/// </summary>
public record AddMarketClosuresCommand(
    string UserId,
    IReadOnlyList<DateTime> Dates,
    string? Note) : IRequest<AddMarketClosuresResult>;

/// <summary>
/// Đếm tách ba nhóm để người gọi biết chuyện gì đã xảy ra. Dán cả năm vào mà chỉ nhận
/// một con số tổng thì không phân biệt được "đã nhập rồi" với "bị bỏ vì cuối tuần".
/// </summary>
public record AddMarketClosuresResult(int Added, int SkippedWeekend, int AlreadyExisted);

public class AddMarketClosuresCommandHandler
    : IRequestHandler<AddMarketClosuresCommand, AddMarketClosuresResult>
{
    private readonly IMarketClosureRepository _repository;

    public AddMarketClosuresCommandHandler(IMarketClosureRepository repository)
        => _repository = repository;

    public async Task<AddMarketClosuresResult> Handle(
        AddMarketClosuresCommand request, CancellationToken cancellationToken)
    {
        int added = 0, skippedWeekend = 0, alreadyExisted = 0;

        foreach (var date in request.Dates.Select(d => d.Date).Distinct())
        {
            // Chặn cuối tuần TẠI ĐÂY thay vì để entity ném: dán cả năm vào mà có một ngày
            // cuối tuần thì cả lô vỡ, trong khi ngày đó vốn đã là ngày nghỉ.
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                skippedWeekend++;
                continue;
            }

            var closure = new MarketClosure(request.UserId, date, request.Note);
            if (await _repository.TryAddAsync(closure, cancellationToken)) added++;
            else alreadyExisted++;
        }

        return new AddMarketClosuresResult(added, skippedWeekend, alreadyExisted);
    }
}
