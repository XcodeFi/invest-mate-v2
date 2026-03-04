using MediatR;
using InvestmentApp.Application.Interfaces;

namespace InvestmentApp.Application.Snapshots.Commands.TakeSnapshot;

public class TakeSnapshotCommand : IRequest<bool>
{
    public string PortfolioId { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class TakeSnapshotCommandHandler : IRequestHandler<TakeSnapshotCommand, bool>
{
    private readonly ISnapshotService _snapshotService;
    private readonly IPortfolioRepository _portfolioRepository;

    public TakeSnapshotCommandHandler(
        ISnapshotService snapshotService,
        IPortfolioRepository portfolioRepository)
    {
        _snapshotService = snapshotService;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<bool> Handle(TakeSnapshotCommand request, CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio == null || portfolio.UserId != request.UserId)
            return false;

        await _snapshotService.TakeSnapshotAsync(request.PortfolioId, cancellationToken);
        return true;
    }
}
