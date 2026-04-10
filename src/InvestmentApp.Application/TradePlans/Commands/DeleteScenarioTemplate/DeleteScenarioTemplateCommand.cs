using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.TradePlans.Commands.DeleteScenarioTemplate;

public class DeleteScenarioTemplateCommand : IRequest
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
}

public class DeleteScenarioTemplateCommandHandler : IRequestHandler<DeleteScenarioTemplateCommand>
{
    private readonly IScenarioTemplateRepository _repository;

    public DeleteScenarioTemplateCommandHandler(IScenarioTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteScenarioTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id);
        if (template == null)
            throw new KeyNotFoundException($"Scenario template '{request.Id}' not found");

        if (template.UserId != request.UserId)
            throw new UnauthorizedAccessException("Cannot delete another user's template");

        await _repository.DeleteAsync(request.Id);
    }
}
