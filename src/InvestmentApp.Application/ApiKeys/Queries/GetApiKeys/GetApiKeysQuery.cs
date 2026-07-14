using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.ApiKeys.Queries.GetApiKeys;

public class GetApiKeysQuery : IRequest<IEnumerable<ApiKeyDto>>
{
    /// <summary>Set server-side from the authenticated principal — never from the request body.</summary>
    public string UserId { get; set; } = null!;
}

public class GetApiKeysQueryHandler : IRequestHandler<GetApiKeysQuery, IEnumerable<ApiKeyDto>>
{
    private readonly IApiKeyRepository _repo;

    public GetApiKeysQueryHandler(IApiKeyRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<ApiKeyDto>> Handle(GetApiKeysQuery request, CancellationToken ct)
    {
        var keys = await _repo.GetByUserIdAsync(request.UserId, ct);
        var now = DateTime.UtcNow;
        return keys.Select(k => MapToDto(k, now));
    }

    private static ApiKeyDto MapToDto(ApiKey k, DateTime asOf) => new()
    {
        Id = k.Id,
        Name = k.Name,
        Prefix = k.Prefix,
        CreatedAt = k.CreatedAt,
        ExpiresAt = k.ExpiresAt,
        LastUsedAt = k.LastUsedAt,
        RevokedAt = k.RevokedAt,
        IsActive = k.IsActive(asOf),
    };
}

/// <summary>Never carries the token or its hash — only display metadata.</summary>
public class ApiKeyDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Prefix { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsActive { get; set; }
}
