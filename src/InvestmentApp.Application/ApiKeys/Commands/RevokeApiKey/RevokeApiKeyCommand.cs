using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.ApiKeys.Commands.RevokeApiKey;

public class RevokeApiKeyCommand : IRequest<Unit>
{
    public string Id { get; set; } = null!;
    /// <summary>Set server-side from the authenticated principal — never from the request body.</summary>
    public string UserId { get; set; } = null!;
}

public class RevokeApiKeyCommandHandler : IRequestHandler<RevokeApiKeyCommand, Unit>
{
    private readonly IApiKeyRepository _repo;

    public RevokeApiKeyCommandHandler(IApiKeyRepository repo)
    {
        _repo = repo;
    }

    public async Task<Unit> Handle(RevokeApiKeyCommand request, CancellationToken ct)
    {
        var apiKey = await _repo.GetByIdAsync(request.Id, ct)
            ?? throw new Exception($"API key {request.Id} not found");

        if (apiKey.UserId != request.UserId)
            throw new UnauthorizedAccessException("Not authorized to revoke this API key");

        apiKey.Revoke();
        await _repo.UpdateAsync(apiKey, ct);
        return Unit.Value;
    }
}
