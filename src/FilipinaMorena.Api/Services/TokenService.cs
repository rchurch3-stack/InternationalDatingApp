using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FilipinaMorena.Api.Contracts;
using FilipinaMorena.Api.Models;
using FilipinaMorena.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FilipinaMorena.Api.Services;

public sealed class TokenService(IOptions<JwtOptions> options)
{
    public AuthResponse Create(AppUser user)
    {
        var expires = DateTime.UtcNow.AddHours(8);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id!),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
            new Claim("mfa", user.IsMfaEnabled.ToString().ToLowerInvariant())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            options.Value.Issuer,
            options.Value.Audience,
            claims,
            expires: expires,
            signingCredentials: credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires,
            new UserResponse(user.Id!, user.Email, user.DisplayName, user.IsMfaEnabled));
    }
}
