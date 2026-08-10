using InvestmentApp.Application.Common.Interfaces;
using MediatR;

namespace InvestmentApp.Application.CompanyDossiers.Commands.ConfirmCompanyDossier;

public class ConfirmCompanyDossierCommand : IRequest
{
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
}

public class ConfirmCompanyDossierCommandHandler : IRequestHandler<ConfirmCompanyDossierCommand>
{
    private readonly ICompanyDossierRepository _repo;

    public ConfirmCompanyDossierCommandHandler(ICompanyDossierRepository repo) => _repo = repo;

    public async Task Handle(ConfirmCompanyDossierCommand request, CancellationToken ct)
    {
        var dossier = await _repo.GetAsync(request.UserId, request.Symbol)
            ?? throw new KeyNotFoundException($"Chưa có hồ sơ cho mã {request.Symbol}");

        dossier.Confirm();
        await _repo.UpdateAsync(dossier);
    }
}
