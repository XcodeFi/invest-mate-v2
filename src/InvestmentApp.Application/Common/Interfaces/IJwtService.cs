using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    string? ValidateToken(string token);
}