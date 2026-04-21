using System.Text.Json.Serialization;
using MediatR;

namespace InvestmentApp.Application.Admin.Queries.SearchUsers;

public class SearchUsersQuery : IRequest<List<AdminUserDto>>
{
    [JsonIgnore]
    public string CallerUserId { get; set; } = null!;
    public string EmailQuery { get; set; } = string.Empty;
}

public class AdminUserDto
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Role { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
