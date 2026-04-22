using System.Text.Json.Serialization;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using MediatR;

namespace InvestmentApp.Application.PersonalFinance.Queries.GetFinancialProfile;

public class GetFinancialProfileQuery : IRequest<FinancialProfileDto?>
{
    [JsonIgnore]
    public string UserId { get; set; } = null!;
}

public class GetFinancialProfileQueryHandler : IRequestHandler<GetFinancialProfileQuery, FinancialProfileDto?>
{
    private readonly IFinancialProfileRepository _repository;

    public GetFinancialProfileQueryHandler(IFinancialProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<FinancialProfileDto?> Handle(GetFinancialProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
        return profile is null ? null : PersonalFinanceMapper.ToDto(profile);
    }
}
