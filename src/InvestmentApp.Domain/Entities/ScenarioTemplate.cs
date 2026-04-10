namespace InvestmentApp.Domain.Entities;

public class ScenarioTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public List<ScenarioNode> Nodes { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
