using MEMS.Api.Auth;
using MEMS.Data;
using MEMS.Data.Services;
using MEMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MEMS.Api.Controllers;

/// <summary>Admin sign-in for API clients.</summary>
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(
    UserManager<ApplicationUser> users,
    IMunicipalityService municipalities,
    JwtTokenService tokens,
    ILogger<AuthController> logger) : ApiControllerBase
{
    /// <summary>Signs in an admin and returns a JWT.</summary>
    /// <remarks>
    /// Example: <c>{ "userName": "burnabyadmin", "password": "P@$$w0rd" }</c>.
    /// The account locks for 5 minutes after 5 wrong passwords. Only admin accounts can sign in.
    /// </remarks>
    /// <response code="200">Signed in; send the token as <c>Authorization: Bearer {token}</c>.</response>
    /// <response code="400">User name or password missing.</response>
    /// <response code="401">Wrong user name/password, locked out, or not an admin account.</response>
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(request.UserName.Trim());
        if (user is null)
        {
            logger.LogWarning("Login failed for unknown user {UserName}", request.UserName);
            return InvalidLogin();
        }

        if (await users.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login attempt for locked-out user {UserName}", user.UserName);
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Too many failed attempts. Try again in a few minutes.");
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            logger.LogWarning("Login failed for user {UserName}: wrong password", user.UserName);
            return InvalidLogin();
        }

        await users.ResetAccessFailedCountAsync(user);

        var roles = await users.GetRolesAsync(user);
        var role = roles.Contains(AppRoles.SuperAdmin) ? AppRoles.SuperAdmin
            : roles.Contains(AppRoles.MunicipalityAdmin) ? AppRoles.MunicipalityAdmin
            : null;
        if (role is null)
        {
            logger.LogWarning("Login refused for {UserName}: not an admin", user.UserName);
            return InvalidLogin();
        }

        string? tenantSlug = null;
        if (user.MunicipalityId is int tenantId)
        {
            tenantSlug = (await municipalities.FindByIdAsync(tenantId, ct))?.Slug;
        }

        var (token, expiresAt) = tokens.CreateToken(user, role);
        logger.LogInformation("Admin {UserName} ({Role}, tenant {Tenant}) signed in to the API", user.UserName, role, tenantSlug ?? "all");

        return new LoginResponse(token, expiresAt, user.UserName!, role, tenantSlug);
    }

    // Same message for unknown user and wrong password, so user names can't be probed.
    private ObjectResult InvalidLogin() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid user name or password.");
}
