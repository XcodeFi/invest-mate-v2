using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvestmentApp.Infrastructure.Services;

/// <summary>
/// Promotes users listed in Admin:AllowEmails to UserRole.Admin on startup.
/// Idempotent: does not override existing Admin role and never demotes.
/// </summary>
public class AdminBootstrapHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminBootstrapHostedService> _logger;

    public AdminBootstrapHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AdminBootstrapHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Admin:AllowEmails is a comma-separated string. Dev value lives in
        // appsettings.Development.json; prod is set via env var Admin__AllowEmails.
        // If the value is the literal placeholder from appsettings.json
        // ("{Admin__AllowEmails}"), treat as not configured.
        var raw = _configuration["Admin:AllowEmails"] ?? string.Empty;
        if (raw.StartsWith('{') && raw.EndsWith('}'))
        {
            _logger.LogInformation("Admin bootstrap: Admin:AllowEmails placeholder not substituted, skipping");
            return;
        }
        var emails = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (emails.Length == 0)
        {
            _logger.LogInformation("Admin bootstrap: no Admin:AllowEmails configured, skipping");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            foreach (var email in emails)
            {
                if (string.IsNullOrWhiteSpace(email)) continue;
                var normalized = email.Trim().ToLowerInvariant();

                var user = await userRepo.GetByEmailAsync(normalized, cancellationToken);
                if (user == null)
                {
                    _logger.LogInformation("Admin bootstrap: no user yet for {Email} — will be promoted on next startup if they sign up", normalized);
                    continue;
                }

                if (user.Role == UserRole.Admin)
                {
                    _logger.LogDebug("Admin bootstrap: {Email} already Admin", normalized);
                    continue;
                }

                user.PromoteToAdmin();
                await userRepo.UpdateAsync(user, cancellationToken);
                _logger.LogInformation("Admin bootstrap: promoted {Email} to Admin", normalized);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin bootstrap failed — app will start anyway; promote manually if needed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
