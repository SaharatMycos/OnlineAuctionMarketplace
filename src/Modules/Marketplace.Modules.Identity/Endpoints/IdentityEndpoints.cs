using Marketplace.Modules.Identity.Domain;
using Marketplace.Modules.Identity.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Identity.Endpoints;

internal static class IdentityEndpoints
{
    /// <summary>Demo accounts offered on the dev sign-in page.</summary>
    public static readonly (string Email, string Name)[] DevUsers =
    [
        ("alice@example.test", "Alice (seller)"),
        ("bob@example.test", "Bob (buyer)"),
        ("carol@example.test", "Carol (buyer)"),
    ];

    public sealed record DevTokenRequest(string Email, string? DisplayName);
    public sealed record MeResponse(Guid Id, string Email, string DisplayName, string Handle);
    public sealed record TokenResponse(string Token, MeResponse User);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/me", GetMe).RequireAuthorization().WithTags("Identity");

        // Dev auth (plan: "dev auth now, real identity provider later"). Off unless Auth:DevTokens is true.
        if (app.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>("Auth:DevTokens"))
        {
            var dev = app.MapGroup("/v1/dev").WithTags("Dev");
            dev.MapPost("/token", IssueDevToken);
            dev.MapGet("/users", () => DevUsers.Select(u => new { u.Email, DisplayName = u.Name }));
        }
    }

    private static async Task<MeResponse> GetMe(ICurrentUser current, IdentityDbContext db, CancellationToken ct)
    {
        var id = current.RequireId();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) ?? throw ProblemException.NotFound("User");
        return new MeResponse(user.Id, user.Email, user.DisplayName, user.Handle);
    }

    /// <summary>Signs in as any email, creating the user on first use.</summary>
    private static async Task<TokenResponse> IssueDevToken(DevTokenRequest request, IdentityDbContext db, DevTokenIssuer issuer, IClock clock, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw ProblemException.BadRequest("invalid_email", "Give an email address.");

        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            var handle = User.HandleFrom(email);
            for (var n = 2; await db.Users.AnyAsync(u => u.Handle == handle, ct); n++)
                handle = $"{User.HandleFrom(email)}{n}";

            var displayName = request.DisplayName?.Trim() is { Length: > 0 } name
                ? name
                : DevUsers.FirstOrDefault(u => u.Email == email).Name ?? handle;
            user = new User { Email = email, DisplayName = displayName, Handle = handle, CreatedAt = clock.UtcNow };
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Two first sign-ins raced; the other one created the user.
                db.ChangeTracker.Clear();
                user = await db.Users.SingleAsync(u => u.Email == email, ct);
            }
        }

        var me = new MeResponse(user.Id, user.Email, user.DisplayName, user.Handle);
        return new TokenResponse(issuer.Issue(user.Id, user.Handle, user.DisplayName), me);
    }
}
