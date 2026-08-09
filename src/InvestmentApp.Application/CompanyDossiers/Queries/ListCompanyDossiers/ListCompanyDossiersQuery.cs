using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.CompanyDossiers.DTOs;
using MediatR;

namespace InvestmentApp.Application.CompanyDossiers.Queries.ListCompanyDossiers;

public class ListCompanyDossiersQuery : IRequest<List<CompanyDossierDto>>
{
    public string UserId { get; set; } = null!;
}

public class ListCompanyDossiersQueryHandler : IRequestHandler<ListCompanyDossiersQuery, List<CompanyDossierDto>>
{
    private readonly ICompanyDossierRepository _repo;

    public ListCompanyDossiersQueryHandler(ICompanyDossierRepository repo) => _repo = repo;

    public async Task<List<CompanyDossierDto>> Handle(ListCompanyDossiersQuery request, CancellationToken ct)
    {
        var dossiers = await _repo.GetByUserIdAsync(request.UserId);
        var now = DateTime.UtcNow;
        return dossiers.Select(d => CompanyDossierDto.FromEntity(d, now)).ToList();
    }
}
