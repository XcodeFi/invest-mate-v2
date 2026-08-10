using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.CompanyDossiers.DTOs;
using MediatR;

namespace InvestmentApp.Application.CompanyDossiers.Queries.GetCompanyDossier;

public class GetCompanyDossierQuery : IRequest<CompanyDossierDto?>
{
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
}

public class GetCompanyDossierQueryHandler : IRequestHandler<GetCompanyDossierQuery, CompanyDossierDto?>
{
    private readonly ICompanyDossierRepository _repo;

    public GetCompanyDossierQueryHandler(ICompanyDossierRepository repo) => _repo = repo;

    public async Task<CompanyDossierDto?> Handle(GetCompanyDossierQuery request, CancellationToken ct)
    {
        var dossier = await _repo.GetAsync(request.UserId, request.Symbol);
        return dossier is null ? null : CompanyDossierDto.FromEntity(dossier, DateTime.UtcNow);
    }
}
