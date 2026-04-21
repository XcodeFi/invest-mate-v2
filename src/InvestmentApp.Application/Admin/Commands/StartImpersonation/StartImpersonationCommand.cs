using System.Text.Json.Serialization;
using MediatR;

namespace InvestmentApp.Application.Admin.Commands.StartImpersonation;

public class StartImpersonationCommand : IRequest<StartImpersonationResult>
{
    [JsonIgnore]
    public string AdminUserId { get; set; } = null!;
    public string TargetUserId { get; set; } = null!;
    public string Reason { get; set; } = null!;
    [JsonIgnore]
    public string IpAddress { get; set; } = string.Empty;
    [JsonIgnore]
    public string UserAgent { get; set; } = string.Empty;
}

public class StartImpersonationResult
{
    public string Token { get; set; } = null!;
    public string ImpersonationId { get; set; } = null!;
    public string TargetEmail { get; set; } = null!;
    public string TargetName { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}
