using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentApp.Api.Controllers;

/// <summary>
/// Base cho các controller anh em trên agent surface (scheme ApiKey). Giữ IMediator + GetUserId()
/// (claim "sub" do ApiKey scheme gắn). Không chứa business logic — mỗi controller con chỉ re-dispatch
/// MediatR command/query sẵn có. AiAgentController gốc không kế thừa base này (ngoài phạm vi, không
/// đụng code đang chạy).
/// </summary>
public abstract class AiAgentControllerBase : ControllerBase
{
    protected readonly IMediator _mediator;

    protected AiAgentControllerBase(IMediator mediator) => _mediator = mediator;

    protected string GetUserId() =>
        User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();
}
