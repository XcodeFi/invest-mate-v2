using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using MediatR;

namespace InvestmentApp.Application.Admin.Queries.SearchUsers;

public class SearchUsersQueryHandler : IRequestHandler<SearchUsersQuery, List<AdminUserDto>>
{
    private const int DefaultLimit = 10;
    private readonly IUserRepository _userRepository;

    public SearchUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<List<AdminUserDto>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        var caller = await _userRepository.GetByIdAsync(request.CallerUserId, cancellationToken);
        if (caller == null || caller.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("Caller is not an admin");

        if (string.IsNullOrWhiteSpace(request.EmailQuery))
            return new List<AdminUserDto>();

        var matches = await _userRepository.SearchByEmailAsync(request.EmailQuery.Trim(), DefaultLimit, cancellationToken);

        return matches
            .Where(u => u.Id != request.CallerUserId)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                Email = u.Email,
                Name = u.Name,
                Role = u.Role.ToString(),
                CreatedAt = u.CreatedAt
            })
            .ToList();
    }
}
