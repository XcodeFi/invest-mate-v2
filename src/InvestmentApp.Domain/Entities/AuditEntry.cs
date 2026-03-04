namespace InvestmentApp.Domain.Entities;

public class AuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string Description { get; set; } = null!;
    public object? Metadata { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}