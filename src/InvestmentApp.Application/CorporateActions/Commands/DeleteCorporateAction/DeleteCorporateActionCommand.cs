using InvestmentApp.Application.Interfaces;
using MediatR;

namespace InvestmentApp.Application.CorporateActions.Commands.DeleteCorporateAction;

public record DeleteCorporateActionCommand(string UserId, string Id) : IRequest<Unit>;

public class DeleteCorporateActionCommandHandler
    : IRequestHandler<DeleteCorporateActionCommand, Unit>
{
    private readonly ICorporateActionRepository _actions;

    public DeleteCorporateActionCommandHandler(ICorporateActionRepository actions)
        => _actions = actions;

    public async Task<Unit> Handle(DeleteCorporateActionCommand request, CancellationToken cancellationToken)
    {
        var action = await _actions.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new ArgumentException("Không tìm thấy sự kiện quyền", nameof(request.Id));
        if (action.UserId != request.UserId)
            throw new UnauthorizedAccessException("Sự kiện quyền không thuộc về người dùng này");
        if (action.CapitalFlowId is not null)
            throw new InvalidOperationException(
                "Sự kiện này đã sinh dòng tiền cổ tức — xoá dòng tiền đó trước rồi mới xoá sự kiện");

        await _actions.DeleteAsync(request.Id, cancellationToken);
        return Unit.Value;
    }
}
