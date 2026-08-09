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

            await CreateOrThrowConflict(created, request.Symbol);
            return created.Id;
        }

        if (request.ByAgent)
            existing.UpdateByAgent(request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);
        else
            existing.UpdateByOwner(request.BusinessModel, request.Moats, request.RiskFactors, request.Notes);

        await _repo.UpdateAsync(existing);
        return existing.Id;
    }

    /// <summary>
    /// Race hiếm: 2 request PUT trùng (userId, symbol) đến gần đồng thời — cả hai GetAsync
    /// ra null rồi cùng CreateAsync, cái sau va unique index ux_user_symbol ở Infrastructure.
    /// Application không phụ thuộc MongoDB.Driver (repository là interface), nên không bắt
    /// theo type MongoWriteException được; nhận diện qua message "E11000" mà driver luôn phát
    /// ra rồi dịch thành InvalidOperationException (đã có sẵn nhánh 409 trong ExceptionMiddleware).
    /// Đơn phiên đơn user nên đây là race hẹp — không cần retry hay lock.
    /// </summary>
    private async Task CreateOrThrowConflict(CompanyDossier dossier, string symbol)
    {
        try
        {
            await _repo.CreateAsync(dossier);
        }
        catch (Exception ex) when (ex.Message.Contains("E11000"))
        {
            throw new InvalidOperationException(
                $"Hồ sơ cho mã {symbol.Trim().ToUpperInvariant()} đã tồn tại, tải lại và cập nhật.", ex);
        }
    }
}
