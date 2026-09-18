using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FilipinaMorena.Api.Models;

public sealed class AppUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public required string Email { get; set; }
    public required string NormalizedEmail { get; set; }
    public required string DisplayName { get; set; }
    public string? PasswordHash { get; set; }
    public List<ExternalLogin> ExternalLogins { get; set; } = [];
    public string? TotpSecret { get; set; }
    public bool IsMfaEnabled { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginUtc { get; set; }
}

public sealed class ExternalLogin
{
    public required string Provider { get; set; }
    public required string ProviderUserId { get; set; }
}

public sealed class ExternalLoginState
{
    [BsonId]
    public required string Id { get; set; }
    public required string Provider { get; set; }
    public required string ReturnUrl { get; set; }
    public DateTime ExpiresUtc { get; set; }
}
