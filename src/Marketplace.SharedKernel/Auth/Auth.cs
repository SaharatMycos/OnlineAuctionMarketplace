using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.SharedKernel.Auth;

/// <summary>
/// JWT settings (section "Auth"). Dev: a symmetric key and our own issuer (Identity's dev token
/// endpoint). Later: set an external identity provider's issuer/audience and remove the key; modules
/// only ever see <see cref="ICurrentUser"/>.
/// </summary>
public sealed class AuthOptions
{
    public string Issuer { get; set; } = "marketplace-dev";
    public string Audience { get; set; } = "marketplace";
    /// <summary>HS256 key, at least 32 bytes. Dev only; never commit a real one.</summary>
    public string? SigningKey { get; set; }
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(12);

    public SymmetricSecurityKey? Key =>
        string.IsNullOrEmpty(SigningKey) ? null : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
}

public static class MarketplaceClaims
{
    public const string UserId = "sub";
    public const string Handle = "handle";
    public const string Name = "name";
}

public interface ICurrentUser
{
    Guid? Id { get; }
    string? Handle { get; }
    bool IsAuthenticated => Id is not null;

    /// <summary>The caller's id; throws <see cref="UnauthorizedAccessException"/> when anonymous.</summary>
    Guid RequireId() => Id ?? throw new UnauthorizedAccessException();
}

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? Id => Guid.TryParse(Principal?.FindFirstValue(MarketplaceClaims.UserId), out var id) ? id : null;
    public string? Handle => Principal?.FindFirstValue(MarketplaceClaims.Handle);
}

/// <summary>
/// Issues dev tokens. Only the Identity module's dev endpoint uses it. Token lifetimes use real time
/// (the same clock the JWT validator uses), not the domain <see cref="IClock"/>.
/// </summary>
public sealed class DevTokenIssuer(IOptions<AuthOptions> options)
{
    public string Issue(Guid userId, string handle, string displayName)
    {
        var o = options.Value;
        var key = o.Key ?? throw new InvalidOperationException("Auth:SigningKey is not configured.");
        var now = DateTimeOffset.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = (now + o.TokenLifetime).UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [MarketplaceClaims.UserId] = userId.ToString(),
                [MarketplaceClaims.Handle] = handle,
                [MarketplaceClaims.Name] = displayName,
            },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }
}

public static class AuthServiceCollectionExtensions
{
    /// <summary>JWT bearer authentication for the api and realtime hosts. Options are read lazily from "Auth".</summary>
    public static IServiceCollection AddMarketplaceAuthentication(this IServiceCollection services)
    {
        services.AddOptions<AuthOptions>().BindConfiguration("Auth");
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<DevTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, auth) =>
            {
                var o = auth.Value;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = o.Issuer,
                    ValidAudience = o.Audience,
                    IssuerSigningKey = o.Key,
                    NameClaimType = MarketplaceClaims.Handle,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                // Browsers can't set headers on WebSockets, so SignalR sends the token in the query string.
                jwt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = token;
                        return Task.CompletedTask;
                    },
                };
            });
        services.AddAuthorization();
        return services;
    }
}
