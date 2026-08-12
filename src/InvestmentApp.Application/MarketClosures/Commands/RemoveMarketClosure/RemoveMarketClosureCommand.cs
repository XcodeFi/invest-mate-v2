using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketClosures.Commands.RemoveMarketClosure;

/// <summary>Xoá một ngày nghỉ — dùng khi lịch nghỉ được điều chỉnh hoặc nhập nhầm.</summary>
public record RemoveMarketClosureCommand(string UserId, DateTime Date) : IRequest<bool>;

public class RemoveMarketClosureCommandHandler : IRequestHandler<RemoveMarketClosureCommand, bool>
{
    private readonly IMarketClosureRepository _repository;

    public RemoveMarketClosureCommandHandler(IMarketClosureRepository repository)
        => _repository = repository;

    public async Task<bool> Handle(RemoveMarketClosureCommand request, CancellationToken cancellationToken)
        => await _repository.DeleteByDateAsync(request.UserId, request.Date, cancellationToken);
}
