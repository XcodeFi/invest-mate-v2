using System.Text.Json.Serialization;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Portfolios.Commands.CreatePortfolio;

public class CreatePortfolioCommand : IRequest<string>
{
    [JsonIgnore]
    public string? UserId { get; set; }
    public string Name { get; set; } = null!;
    public decimal InitialCapital { get; set; }
}