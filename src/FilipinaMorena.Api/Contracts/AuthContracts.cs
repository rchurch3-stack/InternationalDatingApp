using System.ComponentModel.DataAnnotations;

namespace FilipinaMorena.Api.Contracts;

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(2), MaxLength(80)] string DisplayName,
    [Required, MinLength(10), MaxLength(128)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    string? TotpCode);

public sealed record VerifyMfaRequest([Required] string Code);
public sealed record AuthResponse(string AccessToken, DateTime ExpiresUtc, UserResponse User);
public sealed record UserResponse(string Id, string Email, string DisplayName, bool IsMfaEnabled);
public sealed record MfaSetupResponse(string Secret, string OtpAuthUri);
