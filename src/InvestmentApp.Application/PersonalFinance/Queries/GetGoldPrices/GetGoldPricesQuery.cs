using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using MediatR;

namespace InvestmentApp.Application.PersonalFinance.Queries.GetGoldPrices;

/// <summary>
/// Trả bảng giá vàng hiện tại (Miếng + Nhẫn, 4 brand SJC/DOJI/PNJ/Other). Dùng để FE render dropdown + live price
/// trong form Gold account. Public data — không user-specific.
/// </summary>
public class GetGoldPricesQuery : IRequest<IReadOnlyList<GoldPriceDto>>
{
}

public class GetGoldPricesQueryHandler : IRequestHandler<GetGoldPricesQuery, IReadOnlyList<GoldPriceDto>>
{
    private readonly IGoldPriceProvider _goldPriceProvider;

    public GetGoldPricesQueryHandler(IGoldPriceProvider goldPriceProvider)
    {
        _goldPriceProvider = goldPriceProvider;
    }

    public Task<IReadOnlyList<GoldPriceDto>> Handle(GetGoldPricesQuery request, CancellationToken cancellationToken)
    {
        return _goldPriceProvider.GetPricesAsync(cancellationToken);
    }
}
