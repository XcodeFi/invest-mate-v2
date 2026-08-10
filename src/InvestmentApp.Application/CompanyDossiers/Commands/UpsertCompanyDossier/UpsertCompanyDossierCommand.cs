using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;

public class UpsertCompanyDossierCommand : IRequest<string>
{
    public string UserId { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public string BusinessModel { get; set; } = string.Empty;
    public List<MoatItem> Moats { get; set; } = new();
    public List<RiskFactor> RiskFactors { get; set; } = new();
    public string? Notes { get; set; }

    /// <summary>true khi lệnh đến từ MCP. Controller JWT luôn để false.</summary>
    public bool ByAgent { get; set; }
}

public class UpsertCompanyDossierCommandHandler : IRequestHandler<UpsertCompanyDossierCommand, string>
{
    private readonly ICompanyDossierRepository _repo;

    public UpsertCompanyDossierCommandHandler(ICompanyDossierRepository repo) => _repo = repo;

    public async Task<string> Handle(UpsertCompanyDossierCommand request, CancellationToken ct)
    {
        var existing = await _repo.GetAsync(request.UserId, request.Symbol);

        if (existing is null)
        {
            var created = new CompanyDossier(request.UserId, request.Symbol,
                request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);

            if (request.ByAgent)
                created.UpdateByAgent(request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);

            try
            {
                await _repo.CreateAsync(created);
                return created.Id;
            }
            catch (DuplicateDossierException)
            {
                // Một request khác đã tạo xong hồ sơ này giữa lúc ta thấy null và lúc ta
                // insert. Tìm lại ĐÚNG MỘT LẦN rồi rơi xuống đường cập nhật bên dưới —
                // insert lại chỉ va vào đúng index unique đó, và vòng lặp thì đổi 500
                // thành treo request.
                existing = await _repo.GetAsync(request.UserId, request.Symbol);
                if (existing is null) throw;
            }
        }

        if (request.ByAgent)
            existing.UpdateByAgent(request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);
        else
            existing.UpdateByOwner(request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);

        await _repo.UpdateAsync(existing);
        return existing.Id;
    }
}
