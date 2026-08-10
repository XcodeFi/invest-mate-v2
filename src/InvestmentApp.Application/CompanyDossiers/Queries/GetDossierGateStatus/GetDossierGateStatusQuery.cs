using InvestmentApp.Application.CompanyDossiers.DTOs;
using InvestmentApp.Application.CompanyDossiers.Gate;
using InvestmentApp.Application.Common.Interfaces;
using MediatR;

namespace InvestmentApp.Application.CompanyDossiers.Queries.GetDossierGateStatus;

public class GetDossierGateStatusQuery : IRequest<DossierGateStatusDto>
{
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public int? Quantity { get; set; }
    public decimal? EntryPrice { get; set; }
    public decimal? AccountBalance { get; set; }
}

public class GetDossierGateStatusQueryHandler : IRequestHandler<GetDossierGateStatusQuery, DossierGateStatusDto>
{
    private readonly ICompanyDossierGate _gate;
    private readonly ICompanyDossierRepository _repo;

    public GetDossierGateStatusQueryHandler(ICompanyDossierGate gate, ICompanyDossierRepository repo)
    {
        _gate = gate;
        _repo = repo;
    }

    public async Task<DossierGateStatusDto> Handle(GetDossierGateStatusQuery request, CancellationToken ct)
    {
        var planSize = (request.Quantity ?? 0) * (request.EntryPrice ?? 0m);

        var result = await _gate.EvaluateAsync(
            request.UserId, request.Symbol, planSize, request.AccountBalance, ct);

        // NeedsReview không chặn cổng (passed=true) nhưng UI cần biết để nhắc xem lại —
        // map Freshness từ hồ sơ giống CompanyDossierDto.FromEntity đang làm.
        var dossier = await _repo.GetAsync(request.UserId, request.Symbol);

        return new DossierGateStatusDto
        {
            Symbol = request.Symbol.Trim().ToUpperInvariant(),
            Passed = result.Passed,
            Reason = result.Reason,
            Missing = result.Missing,
            Freshness = dossier?.GetFreshness(DateTime.UtcNow).ToString()
        };
    }
}
