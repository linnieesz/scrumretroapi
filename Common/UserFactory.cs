using ScrumRetroApi.Models;

namespace ScrumRetroApi.Common;

public static class UserFactory
{
    public static User Create(string fullName, string normalizedEmail, string? department, string? phone,
        string? bio, UserRole role, string password)
    {
        var (hash, salt) = PasswordHasher.HashPassword(password);
        var now = DateTime.UtcNow;
        return new User
        {
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            Department = InputRules.Clean(department),
            Phone = InputRules.Clean(phone),
            Bio = InputRules.Clean(bio),
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
