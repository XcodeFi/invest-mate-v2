namespace InvestmentApp.Application.Common.Interfaces;

public interface IAiKeyEncryptionService
{
    string Encrypt(string plainTextApiKey);
    string Decrypt(string encryptedApiKey);
}
