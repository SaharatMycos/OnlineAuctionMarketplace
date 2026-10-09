using System.Text.RegularExpressions;

namespace Marketplace.Modules.Identity.Domain;

internal sealed partial class User
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public required string Email { get; init; }
    public required string DisplayName { get; set; }
    /// <summary>Public handle, unique, lowercase. Shown masked to other bidders.</summary>
    public required string Handle { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Handle candidate from the email's local part: lowercase letters, digits and underscores.</summary>
    public static string HandleFrom(string email)
    {
        var local = NormalizeEmail(email).Split('@')[0];
        var handle = NonHandleChars().Replace(local, "_").Trim('_');
        return handle.Length >= 3 ? handle[..Math.Min(handle.Length, 24)] : $"user_{handle}".TrimEnd('_');
    }

    [GeneratedRegex("[^a-z0-9_]+")]
    private static partial Regex NonHandleChars();
}
