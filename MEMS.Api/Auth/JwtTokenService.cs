using System.Security.Claims;
using System.Text;
using MEMS.Data;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MEMS.Api.Auth;

/// <summary>"Jwt" config section. The key is supplied by the AppHost (generated Aspire parameter), never committed.</summary>
public sealed class JwtSettings
{
    public const string RoleClaim = "role";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiresInMinutes { get; set; } = 60;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

/// <summary>
/// Issues admin JWTs (ported from A1's JwtTokenService). Adds the role and the admin's tenant so the API can
/// enforce tenant boundaries without a database lookup per request.
/// </summary>
public sealed class JwtTokenService(JwtSettings settings)
{
    public (string Token, DateTime ExpiresAt) CreateToken(ApplicationUser user, string role)
    {
        var expires = DateTime.UtcNow.AddMinutes(settings.ExpiresInMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtSettings.RoleClaim, role),
        };

        if (user.MunicipalityId is int tenantId)
        {
            claims.Add(new Claim(AppRoles.TenantClaim, tenantId.ToString(), ClaimValueTypes.Integer32));
        }

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256),
        });

        return (token, expires);
    }
}
