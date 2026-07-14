using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.ApiKeys.Commands.CreateApiKey;

public class CreateApiKeyCommand : IRequest<CreatedApiKeyDto>
{
    /// <summary>Set server-side from the authenticated principal — never from the request body.</summary>
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int ExpiresInDays { get; set; } = 90;
}

/// <summary>Returned once at creation. <see cref="Token"/> is the plaintext — shown to the user a single time.</summary>
public class CreatedApiKeyDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Prefix { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateApiKeyCommandHandler : IRequestHandler<CreateApiKeyCommand, CreatedApiKeyDto>
{
    private readonly IApiKeyRepository _repo;
    private readonly IApiKeyTokenService _tokenService;

    public CreateApiKeyCommandHandler(IApiKeyRepository repo, IApiKeyTokenService tokenService)
    {
        _repo = repo;
        _tokenService = tokenService;
    }

    public async Task<CreatedApiKeyDto> Handle(CreateApiKeyCommand request, CancellationToken ct)
    {
        var generated = _tokenService.Generate();
        var expiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays);

        var apiKey = ApiKey.Create(request.UserId, request.Name, generated.Hash, generated.Prefix, expiresAt);
        await _repo.AddAsync(apiKey, ct);

        return new CreatedApiKeyDto
        {
            Id = apiKey.Id,
            Name = apiKey.Name,
            Prefix = apiKey.Prefix,
            Token = generated.Plaintext,
            ExpiresAt = apiKey.ExpiresAt,
            CreatedAt = apiKey.CreatedAt,
        };
    }
}
