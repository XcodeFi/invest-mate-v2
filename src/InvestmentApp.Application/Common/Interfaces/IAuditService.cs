using InvestmentApp.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace InvestmentApp.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}