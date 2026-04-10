using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.TradePlans.Queries.GetTradePlans;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Queries.GetScenarioTemplates;

public class GetScenarioTemplatesQuery : IRequest<List<ScenarioPresetDto>>
{
    public string UserId { get; set; } = null!;
}

public class ScenarioPresetDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string NameVi { get; set; } = null!;
    public string Description { get; set; } = null!;
    public List<ScenarioNodeDto> Nodes { get; set; } = new();
    public bool IsPreset { get; set; }
}

public class GetScenarioTemplatesQueryHandler : IRequestHandler<GetScenarioTemplatesQuery, List<ScenarioPresetDto>>
{
    private readonly IScenarioTemplateRepository _repository;

    public GetScenarioTemplatesQueryHandler(IScenarioTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ScenarioPresetDto>> Handle(GetScenarioTemplatesQuery request, CancellationToken cancellationToken)
    {
        var presets = new List<ScenarioPresetDto>
        {
            BuildConservativePreset(),
            BuildBalancedPreset(),
            BuildAggressivePreset()
        };

        // Merge user templates from DB
        var userTemplates = await _repository.GetByUserIdAsync(request.UserId);
        foreach (var ut in userTemplates)
        {
            presets.Add(new ScenarioPresetDto
            {
                Id = ut.Id,
                Name = ut.Name,
                NameVi = ut.Name, // User templates use their own name
                Description = ut.Description,
                IsPreset = false,
                Nodes = ut.Nodes.Select(n => new ScenarioNodeDto
                {
                    NodeId = n.NodeId,
                    ParentId = n.ParentId,
                    Order = n.Order,
                    Label = n.Label,
                    ConditionType = n.ConditionType.ToString(),
                    ConditionValue = n.ConditionValue,
                    ConditionNote = n.ConditionNote,
                    ActionType = n.ActionType.ToString(),
                    ActionValue = n.ActionValue,
                    TrailingStopConfig = n.TrailingStopConfig != null
                        ? new TrailingStopConfigDto
                        {
                            Method = n.TrailingStopConfig.Method.ToString(),
                            TrailValue = n.TrailingStopConfig.TrailValue,
                            ActivationPrice = n.TrailingStopConfig.ActivationPrice,
                            StepSize = n.TrailingStopConfig.StepSize
                        }
                        : null,
                    Status = "Pending"
                }).ToList()
            });
        }

        return presets;
    }

    private static ScenarioPresetDto BuildConservativePreset() => new()
    {
        Id = "conservative",
        Name = "Conservative",
        NameVi = "An toàn",
        Description = "Chốt lời sớm, cắt lỗ chặt. Phù hợp người mới.",
        IsPreset = true,
        Nodes = new List<ScenarioNodeDto>
        {
            // ROOT-1: Price hits midpoint -> Sell 50%
            new()
            {
                NodeId = "c-root-1",
                ParentId = null,
                Order = 0,
                Label = "Chốt lời 50% tại nửa đường",
                ConditionType = "PricePercentChange",
                ConditionValue = 50,
                ActionType = "SellPercent",
                ActionValue = 50
            },
            // CHILD: Price hits target -> Sell all
            new()
            {
                NodeId = "c-child-1",
                ParentId = "c-root-1",
                Order = 0,
                Label = "Chốt hết tại mục tiêu",
                ConditionType = "PricePercentChange",
                ConditionValue = 100,
                ActionType = "SellAll"
            },
            // ROOT-2: Stop loss -> Sell all
            new()
            {
                NodeId = "c-root-2",
                ParentId = null,
                Order = 1,
                Label = "Cắt lỗ toàn bộ",
                ConditionType = "PriceBelow",
                ConditionValue = 0,
                ActionType = "SellAll"
            }
        }
    };

    private static ScenarioPresetDto BuildBalancedPreset() => new()
    {
        Id = "balanced",
        Name = "Balanced",
        NameVi = "Cân bằng",
        Description = "Chốt lời từng phần, dời SL về hòa vốn, trailing stop. Phù hợp đa số trader.",
        IsPreset = true,
        Nodes = new List<ScenarioNodeDto>
        {
            // ROOT-1: Price 60% to target -> Sell 30%
            new()
            {
                NodeId = "b-root-1",
                ParentId = null,
                Order = 0,
                Label = "Chốt lời 30% (nửa đường)",
                ConditionType = "PricePercentChange",
                ConditionValue = 60,
                ActionType = "SellPercent",
                ActionValue = 30
            },
            // CHILD-1A: Move SL to breakeven
            new()
            {
                NodeId = "b-child-1a",
                ParentId = "b-root-1",
                Order = 0,
                Label = "Dời SL về hòa vốn",
                ConditionType = "PricePercentChange",
                ConditionValue = 60,
                ActionType = "MoveStopToBreakeven"
            },
            // CHILD-1B: Price hits target -> Sell 50%
            new()
            {
                NodeId = "b-child-1b",
                ParentId = "b-root-1",
                Order = 1,
                Label = "Chốt thêm 50% tại mục tiêu",
                ConditionType = "PricePercentChange",
                ConditionValue = 100,
                ActionType = "SellPercent",
                ActionValue = 50
            },
            // GRANDCHILD: Activate trailing stop
            new()
            {
                NodeId = "b-grandchild-1",
                ParentId = "b-child-1b",
                Order = 0,
                Label = "Bật trailing stop 5%",
                ConditionType = "PricePercentChange",
                ConditionValue = 100,
                ActionType = "ActivateTrailingStop",
                TrailingStopConfig = new TrailingStopConfigDto
                {
                    Method = "Percentage",
                    TrailValue = 5
                }
            },
            // Trailing stop triggered -> Sell all
            new()
            {
                NodeId = "b-grandchild-2",
                ParentId = "b-grandchild-1",
                Order = 0,
                Label = "Chốt hết khi chạm trailing",
                ConditionType = "TrailingStopHit",
                ActionType = "SellAll"
            },
            // ROOT-2: Stop loss -> Sell all
            new()
            {
                NodeId = "b-root-2",
                ParentId = null,
                Order = 1,
                Label = "Cắt lỗ toàn bộ",
                ConditionType = "PriceBelow",
                ConditionValue = 0,
                ActionType = "SellAll"
            }
        }
    };

    private static ScenarioPresetDto BuildAggressivePreset() => new()
    {
        Id = "aggressive",
        Name = "Aggressive",
        NameVi = "Tích cực",
        Description = "Trailing stop rộng, cho lợi nhuận chạy. Phù hợp swing/position trading.",
        IsPreset = true,
        Nodes = new List<ScenarioNodeDto>
        {
            // ROOT-1: Target reached -> Sell 30% + trailing
            new()
            {
                NodeId = "a-root-1",
                ParentId = null,
                Order = 0,
                Label = "Chốt 30% + trailing stop 7%",
                ConditionType = "PricePercentChange",
                ConditionValue = 100,
                ActionType = "SellPercent",
                ActionValue = 30,
                TrailingStopConfig = new TrailingStopConfigDto
                {
                    Method = "Percentage",
                    TrailValue = 7
                }
            },
            // CHILD: Trailing hit -> Sell all
            new()
            {
                NodeId = "a-child-1",
                ParentId = "a-root-1",
                Order = 0,
                Label = "Chốt hết khi chạm trailing",
                ConditionType = "TrailingStopHit",
                ActionType = "SellAll"
            },
            // ROOT-2: Early cut 50%
            new()
            {
                NodeId = "a-root-2",
                ParentId = null,
                Order = 1,
                Label = "Cắt lỗ sớm 50% (-5%)",
                ConditionType = "PricePercentChange",
                ConditionValue = -5,
                ActionType = "SellPercent",
                ActionValue = 50
            },
            // CHILD: Full stop
            new()
            {
                NodeId = "a-child-2",
                ParentId = "a-root-2",
                Order = 0,
                Label = "Cắt lỗ toàn bộ",
                ConditionType = "PriceBelow",
                ConditionValue = 0,
                ActionType = "SellAll"
            }
        }
    };
}
