using FluentAssertions;
using InvestmentApp.Api.Mcp;
using InvestmentApp.Application.CompanyDossiers.Commands.UpsertCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.DTOs;
using InvestmentApp.Application.CompanyDossiers.Queries.GetCompanyDossier;
using InvestmentApp.Application.CompanyDossiers.Queries.GetDossierGateStatus;
using InvestmentApp.Application.CompanyDossiers.Queries.ListCompanyDossiers;
using InvestmentApp.Domain.Entities;
using MediatR;
using Moq;

namespace InvestmentApp.Api.Tests.Mcp;

/// <summary>
/// Ghim wiring của 4 tool hồ sơ công ty. `McpToolDiscoveryTests` phủ schema/annotation và chặn
/// việc thêm tool ký, nhưng không phủ được điều quan trọng nhất ở tầng này: mọi tool phải lấy
/// UserId từ claim của khoá API, không bao giờ từ tham số do người gọi truyền vào — một tool bỏ
/// sót là agent của người này đọc/ghi được hồ sơ của người khác.
/// </summary>
public class CompanyDossierToolsTests
{
    private readonly Mock<IMediator> _mediator = new();

    [Fact]
    public async Task ListCompanyDossiers_ScopesToCallerFromApiKeyClaim()
    {
        McpTestContext.Capture<List<CompanyDossierDto>, ListCompanyDossiersQuery>(
            _mediator, out var sent, new List<CompanyDossierDto>());

        await CompanyDossierTools.ListCompanyDossiers(
            _mediator.Object, McpTestContext.WithUser("u-1"), CancellationToken.None);

        sent()!.UserId.Should().Be("u-1");
    }

    [Fact]
    public async Task GetCompanyDossier_ScopesToCallerAndPassesSymbol()
    {
        McpTestContext.Capture<CompanyDossierDto?, GetCompanyDossierQuery>(_mediator, out var sent, null);

        await CompanyDossierTools.GetCompanyDossier(
            "HAH", _mediator.Object, McpTestContext.WithUser("u-2"), CancellationToken.None);

        sent()!.UserId.Should().Be("u-2");
        sent()!.Symbol.Should().Be("HAH");
    }

    [Fact]
    public async Task GetDossierGateStatus_ScopesToCallerAndForwardsAllThreeSizingInputs()
    {
        McpTestContext.Capture<DossierGateStatusDto, GetDossierGateStatusQuery>(
            _mediator, out var sent, new DossierGateStatusDto());

        await CompanyDossierTools.GetDossierGateStatus(
            "HAH", quantity: 10_000, entryPrice: 20_000m, accountBalance: 1_000_000_000m,
            _mediator.Object, McpTestContext.WithUser("u-3"), CancellationToken.None);

        sent()!.UserId.Should().Be("u-3");
        // Ngưỡng đủ nội dung phụ thuộc quy mô lệnh so với số dư, nên rơi một trong ba số là chấm
        // sai bậc — nói "đủ" cho một lệnh thực ra thuộc tầng lớn.
        sent()!.Symbol.Should().Be("HAH");
        sent()!.Quantity.Should().Be(10_000);
        sent()!.EntryPrice.Should().Be(20_000m);
        sent()!.AccountBalance.Should().Be(1_000_000_000m);
    }

    [Fact]
    public async Task UpsertCompanyDossier_ScopesToCaller_AndAlwaysMarksByAgent()
    {
        McpTestContext.Capture<string, UpsertCompanyDossierCommand>(_mediator, out var sent, "dossier-1");

        await CompanyDossierTools.UpsertCompanyDossier(
            "HAH", "Vận tải container nội địa và cho thuê tàu định hạn.",
            _mediator.Object, McpTestContext.WithUser("u-4"), CancellationToken.None);

        sent()!.UserId.Should().Be("u-4");
        // ByAgent = true là thứ kéo ConfirmedAt về null (Q10). Nếu cửa MCP quên cờ này thì agent
        // sửa được nội dung mà chữ ký cũ vẫn còn hiệu lực — đúng định nghĩa cửa hậu của quy tắc
        // "chỉ con người ký".
        sent()!.ByAgent.Should().BeTrue();
        sent()!.Symbol.Should().Be("HAH");
    }

    [Fact]
    public async Task UpsertCompanyDossier_OmittedOptionals_BecomeEmptyCollectionsNotNull()
    {
        McpTestContext.Capture<string, UpsertCompanyDossierCommand>(_mediator, out var sent, "dossier-2");

        await CompanyDossierTools.UpsertCompanyDossier(
            "HAH", "Mô tả cơ chế kiếm tiền dài hơn ba mươi ký tự cho chắc.",
            _mediator.Object, McpTestContext.WithUser("u-5"), CancellationToken.None);

        // Command nhận List không nullable; đẩy null xuống là NRE ở handler chứ không phải 400.
        sent()!.Moats.Should().NotBeNull().And.BeEmpty();
        sent()!.RiskFactors.Should().NotBeNull().And.BeEmpty();
        sent()!.Notes.Should().BeNull();
    }

    [Fact]
    public async Task UpsertCompanyDossier_ForwardsMoatsAndRiskFactorsUnchanged()
    {
        McpTestContext.Capture<string, UpsertCompanyDossierCommand>(_mediator, out var sent, "dossier-3");
        var moats = new List<MoatItem> { new() { Description = "Đội tàu container lớn nhất trong nước." } };
        var risks = new List<RiskFactor>
        {
            new() { Rank = 1, Description = "Giá cước giảm sâu", ObservableSignal = "SCFI giảm quá 30% cùng kỳ", IsDealBreaker = true }
        };

        await CompanyDossierTools.UpsertCompanyDossier(
            "HAH", "Vận tải container nội địa và cho thuê tàu định hạn.",
            _mediator.Object, McpTestContext.WithUser("u-6"), CancellationToken.None,
            moats: moats, riskFactors: risks, notes: "ghi chú");

        sent()!.Moats.Should().BeSameAs(moats);
        sent()!.RiskFactors.Should().BeSameAs(risks);
        sent()!.Notes.Should().Be("ghi chú");
    }
}
