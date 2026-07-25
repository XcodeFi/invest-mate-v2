using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>Fake IHttpContextAccessor mang claim "sub" (mirror AiAgentControllerTests.Sut()) + Moq dispatch-capture helper.</summary>
public static class McpTestContext
{
    public static IHttpContextAccessor WithUser(string userId = "user-1")
    {
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId) }, "ApiKey");
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextAccessor { HttpContext = ctx };
    }

    /// <summary>Setup IMediator.Send to capture the dispatched command of type TConcrete and return `returns`.</summary>
    public static void Capture<TResponse, TConcrete>(
        Mock<IMediator> mock, out Func<TConcrete?> sent, TResponse returns)
        where TConcrete : class, IRequest<TResponse>
    {
        TConcrete? captured = null;
        mock.Setup(m => m.Send(It.IsAny<TConcrete>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<TResponse>, CancellationToken>((c, _) => captured = (TConcrete)c)
            .ReturnsAsync(returns);
        sent = () => captured;
    }
}
