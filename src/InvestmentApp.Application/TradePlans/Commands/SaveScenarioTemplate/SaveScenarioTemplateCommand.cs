using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
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
        var template = new ScenarioTemplate
        {
            Id = Guid.NewGuid().ToString(),
            UserId = request.UserId,
            Name = request.Name,
            Description = request.Description,
            Nodes = request.Nodes.Select(n => new ScenarioNode
            {
                NodeId = n.NodeId,
                ParentId = n.ParentId,
                Order = n.Order,
                Label = n.Label,
                ConditionType = Enum.Parse<ScenarioConditionType>(n.ConditionType),
                ConditionValue = n.ConditionValue,
                ConditionNote = n.ConditionNote,
                ActionType = Enum.Parse<ScenarioActionType>(n.ActionType),
                ActionValue = n.ActionValue,
                TrailingStopConfig = n.TrailingStopConfig != null
                    ? new TrailingStopConfig
                    {
                        Method = Enum.Parse<TrailingStopMethod>(n.TrailingStopConfig.Method),
                        TrailValue = n.TrailingStopConfig.TrailValue,
                        ActivationPrice = n.TrailingStopConfig.ActivationPrice,
                        StepSize = n.TrailingStopConfig.StepSize
                    }
                    : null,
                Status = ScenarioNodeStatus.Pending
            }).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(template);
        return template.Id;
    }
}
