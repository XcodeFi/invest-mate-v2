using InvestmentApp.Application.CompanyDossiers.DTOs;
using InvestmentApp.Application.CompanyDossiers.Gate;
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

    public GetDossierGateStatusQueryHandler(ICompanyDossierGate gate) => _gate = gate;

    public async Task<DossierGateStatusDto> Handle(GetDossierGateStatusQuery request, CancellationToken ct)
    {
        var planSize = (request.Quantity ?? 0) * (request.EntryPrice ?? 0m);

        var result = await _gate.EvaluateAsync(
            request.UserId, request.Symbol, planSize, request.AccountBalance, ct);

        return new DossierGateStatusDto
        {
            Symbol = request.Symbol.Trim().ToUpperInvariant(),
            Passed = result.Passed,
            Reason = result.Reason,
            Missing = result.Missing
        };
    }
}
