using System.Security.Claims;
using FluentAssertions;
using InvestmentApp.Api.Controllers;
using InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;
using InvestmentApp.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestmentApp.Api.Tests.Controllers;

/// <summary>
/// <see cref="CompanyDossier.ConfirmedAt"/> is a human's signature. <c>ByAgent</c> is what
/// decides whether an upsert wipes it (<see cref="CompanyDossier.UpdateByAgent"/>) or keeps it
/// (<see cref="CompanyDossier.UpdateByOwner"/>). The controller is the JWT (human) surface, so it
/// must never let the request body decide that flag. This test pins it at the controller layer —
/// a handler-layer test alone would keep passing even if the controller started binding it.
/// </summary>
public class CompanyDossiersControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private CompanyDossiersController Sut(string userId = "user-1")
    {
        var controller = new CompanyDossiersController(_mediator.Object);
        var identity = new ClaimsIdentity(new[] { new Claim("sub", userId) }, "Bearer");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task Upsert_AlwaysSetsByAgentFalse_RegardlessOfRequestBody()
    {
        UpsertCompanyDossierCommand? sent = null;
        _mediator.Setup(m => m.Send(It.IsAny<UpsertCompanyDossierCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<string>, CancellationToken>((c, _) => sent = (UpsertCompanyDossierCommand)c)
            .ReturnsAsync("dossier-1");

        var request = new UpsertCompanyDossierRequest
        {
            BusinessModel = "Bán thép xây dựng và HRC cho nhà thầu nội địa",
            Moats = new List<MoatItem> { new() { Description = "Chi phí đơn vị thấp nhất ngành" } },
            RiskFactors = new List<RiskFactor>
            {
                new() { Rank = 1, Description = "Giá HRC Trung Quốc", ObservableSignal = "Giá HRC giảm quá 10% trong một tháng" }
            }
        };

        await Sut().Upsert("HPG", request, default);

        sent!.ByAgent.Should().BeFalse();
        sent.UserId.Should().Be("user-1");
        sent.Symbol.Should().Be("HPG");
    }

    [Fact]
    public async Task Upsert_WhenBodyIsNull_ReturnsBadRequest_AndDoesNotDispatch()
    {
        var result = await Sut().Upsert("HPG", null, default);

        result.Should().BeOfType<BadRequestObjectResult>();
        _mediator.Verify(m => m.Send(It.IsAny<UpsertCompanyDossierCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
