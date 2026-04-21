using FluentAssertions;
using Moq;
using InvestmentApp.Application.Admin.Commands.StartImpersonation;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Tests.Admin;

public class StartImpersonationCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IImpersonationAuditRepository> _auditRepo;
    private readonly Mock<IJwtService> _jwtService;
    private readonly Mock<IAuditService> _auditLog;
    private readonly StartImpersonationCommandHandler _handler;

    public StartImpersonationCommandHandlerTests()
    {
        _userRepo = new Mock<IUserRepository>();
        _auditRepo = new Mock<IImpersonationAuditRepository>();
        _jwtService = new Mock<IJwtService>();
        _auditLog = new Mock<IAuditService>();
        _handler = new StartImpersonationCommandHandler(
            _userRepo.Object,
            _auditRepo.Object,
            _jwtService.Object,
            _auditLog.Object);
    }

    private static User NewUser(string id, string email, UserRole role)
    {
        var user = new User(email, $"Name {id}", null, "google");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        if (role == UserRole.Admin) user.PromoteToAdmin();
        return user;
    }

    [Fact]
    public async Task Handle_NonAdminCaller_ThrowsUnauthorized()
    {
        var caller = NewUser("caller-1", "regular@example.com", UserRole.User);
        var target = NewUser("target-1", "target@example.com", UserRole.User);
        _userRepo.Setup(r => r.GetByIdAsync("caller-1", It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _userRepo.Setup(r => r.GetByIdAsync("target-1", It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var command = new StartImpersonationCommand
        {
            AdminUserId = "caller-1",
            TargetUserId = "target-1",
            Reason = "Debug",
            IpAddress = "127.0.0.1",
            UserAgent = "test"
        };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_TargetNotFound_ThrowsArgumentException()
    {
        var caller = NewUser("admin-1", "admin@example.com", UserRole.Admin);
        _userRepo.Setup(r => r.GetByIdAsync("admin-1", It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _userRepo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var command = new StartImpersonationCommand
        {
            AdminUserId = "admin-1",
            TargetUserId = "missing",
            Reason = "Debug",
            IpAddress = "127.0.0.1",
            UserAgent = "test"
        };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Target user*");
    }

    [Fact]
    public async Task Handle_AdminImpersonatingSelf_ThrowsArgumentException()
    {
        var caller = NewUser("admin-1", "admin@example.com", UserRole.Admin);
        _userRepo.Setup(r => r.GetByIdAsync("admin-1", It.IsAny<CancellationToken>())).ReturnsAsync(caller);

        var command = new StartImpersonationCommand
        {
            AdminUserId = "admin-1",
            TargetUserId = "admin-1",
            Reason = "Debug",
            IpAddress = "127.0.0.1",
            UserAgent = "test"
        };

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*self*");
    }

    [Fact]
    public async Task Handle_ValidAdminAndTarget_CreatesAuditAndReturnsToken()
    {
        var caller = NewUser("admin-1", "admin@example.com", UserRole.Admin);
        var target = NewUser("target-1", "target@example.com", UserRole.User);
        _userRepo.Setup(r => r.GetByIdAsync("admin-1", It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _userRepo.Setup(r => r.GetByIdAsync("target-1", It.IsAny<CancellationToken>())).ReturnsAsync(target);

        ImpersonationAudit? savedAudit = null;
        _auditRepo.Setup(r => r.AddAsync(It.IsAny<ImpersonationAudit>(), It.IsAny<CancellationToken>()))
            .Callback<ImpersonationAudit, CancellationToken>((a, _) => savedAudit = a)
            .Returns(Task.CompletedTask);

        _jwtService.Setup(j => j.CreateImpersonationToken(
                "admin-1",
                It.Is<User>(u => u.Id == "target-1"),
                It.IsAny<string>()))
            .Returns("fake-jwt");

        var command = new StartImpersonationCommand
        {
            AdminUserId = "admin-1",
            TargetUserId = "target-1",
            Reason = "Debug issue #123",
            IpAddress = "127.0.0.1",
            UserAgent = "Mozilla/5.0"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Token.Should().Be("fake-jwt");
        result.ImpersonationId.Should().NotBeNullOrEmpty();
        result.TargetEmail.Should().Be("target@example.com");
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromMinutes(1));

        savedAudit.Should().NotBeNull();
        savedAudit!.AdminUserId.Should().Be("admin-1");
        savedAudit.TargetUserId.Should().Be("target-1");
        savedAudit.Reason.Should().Be("Debug issue #123");
        savedAudit.IsRevoked.Should().BeFalse();
    }
}
