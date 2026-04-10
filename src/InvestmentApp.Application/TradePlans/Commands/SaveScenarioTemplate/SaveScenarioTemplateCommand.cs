using InvestmentApp.Application.Common.Interfaces;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.SaveScenarioTemplate;

public class SaveScenarioTemplateCommand : IRequest<string>
{
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public List<SaveScenarioNodeDto> Nodes { get; set; } = new();
}

public class SaveScenarioNodeDto
{
    public string NodeId { get; set; } = null!;
    public string? ParentId { get; set; }
    public int Order { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ConditionType { get; set; } = "PriceAbove";
    public decimal? ConditionValue { get; set; }
    public string? ConditionNote { get; set; }
    public string ActionType { get; set; } = "SellPercent";
    public decimal? ActionValue { get; set; }
    public SaveTrailingStopConfigDto? TrailingStopConfig { get; set; }
}

public class SaveTrailingStopConfigDto
{
    public string Method { get; set; } = "Percentage";
    public decimal TrailValue { get; set; }
    public decimal? ActivationPrice { get; set; }
    public decimal? StepSize { get; set; }
}

public class SaveScenarioTemplateCommandHandler : IRequestHandler<SaveScenarioTemplateCommand, string>
{
    private readonly IScenarioTemplateRepository _repository;

    public SaveScenarioTemplateCommandHandler(IScenarioTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> Handle(SaveScenarioTemplateCommand request, CancellationToken cancellationToken)
    {
        var nodes = new List<Domain.Entities.ScenarioNode>();
        foreach (var n in request.Nodes)
        {
            if (!Enum.TryParse<Domain.Entities.ScenarioConditionType>(n.ConditionType, out var conditionType))
                throw new ArgumentException($"Invalid ConditionType: {n.ConditionType}");
            if (!Enum.TryParse<Domain.Entities.ScenarioActionType>(n.ActionType, out var actionType))
                throw new ArgumentException($"Invalid ActionType: {n.ActionType}");

            Domain.Entities.TrailingStopConfig? trailingConfig = null;
            if (n.TrailingStopConfig != null)
            {
                if (!Enum.TryParse<Domain.Entities.TrailingStopMethod>(n.TrailingStopConfig.Method, out var method))
                    throw new ArgumentException($"Invalid TrailingStopMethod: {n.TrailingStopConfig.Method}");
                trailingConfig = new Domain.Entities.TrailingStopConfig
                {
                    Method = method,
                    TrailValue = n.TrailingStopConfig.TrailValue,
                    ActivationPrice = n.TrailingStopConfig.ActivationPrice,
                    StepSize = n.TrailingStopConfig.StepSize
                };
            }

            nodes.Add(new Domain.Entities.ScenarioNode
            {
                NodeId = n.NodeId,
                ParentId = n.ParentId,
                Order = n.Order,
                Label = n.Label,
                ConditionType = conditionType,
                ConditionValue = n.ConditionValue,
                ConditionNote = n.ConditionNote,
                ActionType = actionType,
                ActionValue = n.ActionValue,
                TrailingStopConfig = trailingConfig,
                Status = Domain.Entities.ScenarioNodeStatus.Pending
            });
        }

        var template = new Domain.Entities.ScenarioTemplate
        {
            Id = Guid.NewGuid().ToString(),
            UserId = request.UserId,
            Name = request.Name,
            Description = request.Description,
            Nodes = nodes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(template);
        return template.Id;
    }
}
