using InvestmentApp.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace InvestmentApp.Infrastructure.Services;

public class AiChatServiceFactory : IAiChatServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public AiChatServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IAiChatService GetService(string provider) => provider switch
    {
        "gemini" => _serviceProvider.GetRequiredService<GeminiApiService>(),
        _ => _serviceProvider.GetRequiredService<ClaudeApiService>(),
    };
}
