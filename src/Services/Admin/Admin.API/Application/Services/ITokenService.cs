using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Admin.API.Application.Services;

public interface ITokenService
{
    (string Token, DateTime Expiry) GenerateJwt(User user, string[] roles, string[] permissions);
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, string? ipAddress);
}

public class TokenService : ITokenService
{
    private readonly AdminDbContext _db;
    private readonly IConfiguration _config;

    public TokenService(AdminDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public (string Token, DateTime Expiry) GenerateJwt(User user, string[] roles, string[] permissions)
    {
        var secretKey = _config["Jwt:SecretKey"] 
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        var expiryMinutes = _config.GetValue<int>("Jwt:ExpiryMinutes", 60);

        var key = Encoding.ASCII.GetBytes(secretKey);
        var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiry,
            Issuer = _config["Jwt:Issuer"] ?? "bizcore-admin",
            Audience = _config["Jwt:Audience"] ?? "bizcore-erp",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return (handler.WriteToken(token), expiry);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, string? ipAddress)
    {
        var expiryDays = _config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var tokenStr = Convert.ToBase64String(tokenBytes);

        var refreshToken = RefreshToken.Create(userId, tokenStr, expiryDays, ipAddress);
        _db.RefreshTokens.Add(refreshToken);
        return refreshToken;
    }
}
