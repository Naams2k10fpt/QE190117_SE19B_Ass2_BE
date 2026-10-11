using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TaskTrack.Repo.Models;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.Service.Services;

public class TokenService : ITokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _signingCredentials;

    public TokenService(IConfiguration configuration)
    {
        _issuer = configuration["Jwt:Issuer"] ?? string.Empty;
        _audience = configuration["Jwt:Audience"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_issuer) ||
            string.IsNullOrWhiteSpace(_audience))
        {
            throw new InvalidOperationException(
                "JWT issuer and audience must be configured.");
        }

        var secret = configuration["JWT_SECRET"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "JWT_SECRET must be configured.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT_SECRET must contain at least 32 UTF-8 bytes.");
        }

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public string CreateAccessToken(
        SystemAccount account,
        Guid sessionId,
        DateTime expiresAt)
    {
        if (expiresAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Token expiry must use UTC.", nameof(expiresAt));
        }

        var claims = new[]
        {
            new Claim("AccountID",
                account.AccountId.ToString(CultureInfo.InvariantCulture)),
            new Claim("Email", account.Email),
            new Claim("Role",
                account.Role.ToString(CultureInfo.InvariantCulture)),
            new Claim("SessionID", sessionId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}