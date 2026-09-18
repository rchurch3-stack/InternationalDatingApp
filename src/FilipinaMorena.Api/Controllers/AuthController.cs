using System.Security.Claims;
using FilipinaMorena.Api.Contracts;
using FilipinaMorena.Api.Data;
using FilipinaMorena.Api.Models;
using FilipinaMorena.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace FilipinaMorena.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    MongoContext db,
    IPasswordHasher<AppUser> passwordHasher,
    TokenService tokenService,
    TotpService totpService,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.Find(x => x.NormalizedEmail == email).AnyAsync(cancellationToken))
            return Conflict(new { message = "An account with that email already exists." });

        var user = new AppUser
        {
            Email = request.Email.Trim(),
            NormalizedEmail = email,
            DisplayName = request.DisplayName.Trim()
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        await db.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
        return Ok(tokenService.Create(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Find(x => x.NormalizedEmail == email).FirstOrDefaultAsync(cancellationToken);
        if (user?.PasswordHash is null ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        if (user.IsMfaEnabled && (string.IsNullOrWhiteSpace(request.TotpCode) ||
            user.TotpSecret is null || !totpService.Verify(user.TotpSecret, request.TotpCode)))
            return Unauthorized(new { message = "A valid 6-digit authenticator code is required.", mfaRequired = true });

        user.LastLoginUtc = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: cancellationToken);
        return Ok(tokenService.Create(user));
    }

    [HttpGet("external/{provider}")]
    public async Task<IActionResult> ExternalLogin(string provider, [FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        var scheme = provider.ToLowerInvariant() switch
        {
            "google" => "Google",
            "facebook" => "Facebook",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(scheme)) return BadRequest(new { message = "Unsupported login provider." });

        var providerIsConfigured = scheme switch
        {
            "Google" => !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"])
                && !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]),
            "Facebook" => !string.IsNullOrWhiteSpace(configuration["Authentication:Facebook:AppId"])
                && !string.IsNullOrWhiteSpace(configuration["Authentication:Facebook:AppSecret"]),
            _ => false
        };
        if (!providerIsConfigured)
            return Problem($"{scheme} login has not been configured.", statusCode: StatusCodes.Status503ServiceUnavailable);

        var frontendUrl = configuration["FrontendUrl"] ?? "http://localhost:4200";
        var configuredOrigin = new Uri(frontendUrl).GetLeftPart(UriPartial.Authority);
        var suppliedOrigin = Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsedReturnUrl)
            ? parsedReturnUrl.GetLeftPart(UriPartial.Authority)
            : string.Empty;
        var safeReturnUrl = string.Equals(configuredOrigin, suppliedOrigin, StringComparison.OrdinalIgnoreCase)
            ? parsedReturnUrl!.ToString()
            : $"{frontendUrl.TrimEnd('/')}/auth/callback";
        var state = new ExternalLoginState
        {
            Id = Guid.NewGuid().ToString("N"),
            Provider = scheme,
            ReturnUrl = safeReturnUrl,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(10)
        };
        await db.ExternalLoginStates.InsertOneAsync(state, cancellationToken: cancellationToken);

        var callback = Url.Action(nameof(ExternalCallback), values: new { provider = scheme, state = state.Id })!;
        return Challenge(new AuthenticationProperties { RedirectUri = callback }, scheme);
    }

    [HttpGet("external/callback")]
    public async Task<IActionResult> ExternalCallback(string provider, string state, CancellationToken cancellationToken)
    {
        var stateRecord = await db.ExternalLoginStates.FindOneAndDeleteAsync(
            x => x.Id == state && x.Provider == provider && x.ExpiresUtc > DateTime.UtcNow,
            cancellationToken: cancellationToken);
        if (stateRecord is null) return BadRequest(new { message = "The external login request expired or is invalid." });

        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
            return Redirect($"{stateRecord.ReturnUrl}?error=external_login_failed");

        var providerUserId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var displayName = result.Principal.FindFirstValue(ClaimTypes.Name) ?? "New member";
        if (string.IsNullOrWhiteSpace(email))
            return Redirect($"{stateRecord.ReturnUrl}?error=email_required");

        var normalizedEmail = email.ToLowerInvariant();
        var user = await db.Users.Find(x =>
            x.ExternalLogins.Any(login => login.Provider == provider && login.ProviderUserId == providerUserId))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            if (await db.Users.Find(x => x.NormalizedEmail == normalizedEmail).AnyAsync(cancellationToken))
                return Redirect($"{stateRecord.ReturnUrl}?error=account_exists");

            user = new AppUser { Email = email, NormalizedEmail = normalizedEmail, DisplayName = displayName };
            user.ExternalLogins.Add(new ExternalLogin { Provider = provider, ProviderUserId = providerUserId });
            await db.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
        }

        var auth = tokenService.Create(user);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect($"{stateRecord.ReturnUrl}#access_token={Uri.EscapeDataString(auth.AccessToken)}");
    }

    [Authorize]
    [HttpPost("mfa/setup")]
    public async Task<ActionResult<MfaSetupResponse>> SetupMfa(CancellationToken cancellationToken)
    {
        var user = await CurrentUser(cancellationToken);
        if (user is null) return Unauthorized();
        user.TotpSecret = totpService.GenerateSecret();
        user.IsMfaEnabled = false;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: cancellationToken);
        var issuer = Uri.EscapeDataString("FilipinaMorena");
        var account = Uri.EscapeDataString(user.Email);
        return Ok(new MfaSetupResponse(user.TotpSecret, $"otpauth://totp/{issuer}:{account}?secret={user.TotpSecret}&issuer={issuer}"));
    }

    [Authorize]
    [HttpPost("mfa/verify")]
    public async Task<IActionResult> VerifyMfa(VerifyMfaRequest request, CancellationToken cancellationToken)
    {
        var user = await CurrentUser(cancellationToken);
        if (user?.TotpSecret is null || !totpService.Verify(user.TotpSecret, request.Code))
            return BadRequest(new { message = "That authenticator code is invalid." });
        user.IsMfaEnabled = true;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: cancellationToken);
        return NoContent();
    }

    private Task<AppUser?> CurrentUser(CancellationToken cancellationToken)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return db.Users.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
    }
}
