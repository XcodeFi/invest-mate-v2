using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.MarketData.Queries.GetSignalSetups;

public class GetSignalSetupsQuery : IRequest<SignalSetupsDto>
{
    public string Symbol { get; set; } = null!;
}

public class GetSignalSetupsQueryHandler : IRequestHandler<GetSignalSetupsQuery, SignalSetupsDto>
{
    private readonly ISignalSetupService _service;

    public GetSignalSetupsQueryHandler(ISignalSetupService service) => _service = service;

    public async Task<SignalSetupsDto> Handle(GetSignalSetupsQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Cần nhập mã chứng khoán (symbol), ví dụ FPT.", nameof(request));

        return await _service.GetSignalSetupsAsync(request.Symbol.ToUpperInvariant().Trim(), ct);
    }
}
