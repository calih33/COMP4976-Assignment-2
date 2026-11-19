using System.ComponentModel.DataAnnotations;

namespace MEMS.Shared.Dtos;

/// <summary>Admin credentials.</summary>
public sealed class LoginRequest
{
    [Required]
    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>A signed JWT to send as <c>Authorization: Bearer {token}</c>.</summary>
/// <param name="Token">The JWT.</param>
/// <param name="ExpiresAt">Expiry time (UTC).</param>
/// <param name="UserName">Signed-in admin.</param>
/// <param name="Role">"Super Admin" or "Municipality Admin".</param>
/// <param name="Tenant">The admin's tenant slug, or null for a Super Admin.</param>
public sealed record LoginResponse(string Token, DateTime ExpiresAt, string UserName, string Role, string? Tenant);
