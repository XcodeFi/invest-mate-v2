namespace InvestmentApp.Application.Common.Interfaces;

public interface IAiChatServiceFactory
{
    IAiChatService GetService(string provider);
}
