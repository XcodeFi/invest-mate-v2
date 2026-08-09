using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Commands.DeleteCorporateAction;

public record DeleteCorporateActionCommand(string UserId, string Id) : IRequest<Unit>;

public class DeleteCorporateActionCommandHandler
    : IRequestHandler<DeleteCorporateActionCommand, Unit>
{
    private readonly ICorporateActionRepository _actions;
    private readonly ICapitalFlowRepository _flows;

    public DeleteCorporateActionCommandHandler(
        ICorporateActionRepository actions, ICapitalFlowRepository flows)
    {
        _actions = actions;
        _flows = flows;
    }

    public async Task<Unit> Handle(DeleteCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var action = await _actions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy sự kiện quyền", nameof(request.Id));
        if (action.UserId != request.UserId)
            throw new UnauthorizedAccessException("Sự kiện quyền không thuộc về người dùng này");

        // Dòng tiền cổ tức do chính sự kiện này sinh ra thì xoá theo, không để mồ côi.
        // Dòng tiền người dùng tự nhập rồi liên kết vào thì giữ lại, chỉ gỡ liên kết.
        if (action.CapitalFlowId is { } flowId)
        {
            var flow = await _flows.GetByIdAsync(flowId, cancellationToken);
            if (flow != null && flow.UserId == request.UserId && flow.PortfolioId == action.PortfolioId)
                await _flows.DeleteAsync(flowId, cancellationToken);
        }

        await _actions.DeleteAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}
