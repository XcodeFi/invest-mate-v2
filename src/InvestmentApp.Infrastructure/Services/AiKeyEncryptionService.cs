using InvestmentApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace InvestmentApp.Infrastructure.Services;

public class AiKeyEncryptionService : IAiKeyEncryptionService
{
    private readonly IDataProtector _protector;

    public AiKeyEncryptionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("AiSettings.ApiKey.v1");
    }

    public string Encrypt(string plainTextApiKey) => _protector.Protect(plainTextApiKey);

    public string Decrypt(string encryptedApiKey) => _protector.Unprotect(encryptedApiKey);
}
