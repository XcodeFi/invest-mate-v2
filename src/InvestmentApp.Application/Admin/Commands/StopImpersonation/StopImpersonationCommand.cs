using System.Text.Json.Serialization;
using MediatR;

namespace InvestmentApp.Application.Admin.Commands.StopImpersonation;

public class StopImpersonationCommand : IRequest<Unit>
{
    public string ImpersonationId { get; set; } = null!;
    [JsonIgnore]
    public string AdminUserId { get; set; } = null!;
}
