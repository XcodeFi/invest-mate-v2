using System;
using MongoDB.Bson.Serialization.Attributes;

namespace InvestmentApp.Domain.Entities;

public class User : AggregateRoot
{
    public string Email { get; private set; }
    public string Name { get; private set; }
    public string? Avatar { get; private set; }
    public string Provider { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public UserRole Role { get; private set; } = UserRole.User;

    [BsonConstructor]
    public User() { } // For EF/MongoDB

    public User(string email, string name, string? avatar, string provider)
    {
        Id = Guid.NewGuid().ToString();
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Avatar = avatar;
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        CreatedAt = DateTime.UtcNow;
        IsDeleted = false;
        Role = UserRole.User;
    }

    public void UpdateProfile(string name, string? avatar)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Avatar = avatar;
    }

    public void MarkAsDeleted()
    {
        IsDeleted = true;
    }

    public void PromoteToAdmin()
    {
        Role = UserRole.Admin;
    }

    public void DemoteToUser()
    {
        Role = UserRole.User;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }
}
