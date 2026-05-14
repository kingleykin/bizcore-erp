using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Admin.API.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly AdminDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthService> _logger;
        private readonly IAuditPublisher _audit;

        public AuthService(
            AdminDbContext db,
            IConfiguration config,
            ILogger<AuthService> logger,
            IAuditPublisher audit)
        {
            _db     = db;
            _config = config;
            _logger = logger;
            _audit  = audit;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress = null)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.Username == request.Username.ToLowerInvariant());

            if (user == null)
            {
                _logger.LogWarning("Login failed: user '{Username}' not found.", request.Username);
                throw new UnauthorizedException("Invalid username or password.");
            }

            if (!user.IsActive)
                throw new UnauthorizedException("Account is deactivated. Please contact an administrator.");

            if (user.IsLockedOut())
                throw new UnauthorizedException($"Account is locked until {user.LockoutEnd:O}. Please try again later.");

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                user.RecordFailedLogin();
                await _db.SaveChangesAsync();
                _logger.LogWarning("Login failed: invalid password for '{Username}'. Attempts: {Attempts}",
                    user.Username, user.FailedLoginAttempts);

                await _audit.PublishAsync(
                    AuditActions.Identity.AuthLoginFailed,
                    entityType: nameof(User), entityId: user.Id.ToString(),
                    after: new { user.Username, user.FailedLoginAttempts },
                    category: AuditCategory.Security,
                    severity: AuditSeverity.Warning,
                    outcome: AuditOutcome.Failure,
                    classification: DataClassification.Credential,
                    actorUsername: request.Username);

                throw new UnauthorizedException("Invalid username or password.");
            }

            // Reset failed attempts on success
            user.ResetFailedLogins();

            // Build claims
            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
            var permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Code)   // Dùng Code (Invoice.View) thay vì Action cũ
                .Distinct()
                .ToArray();

            var (accessToken, expiry) = GenerateJwt(user, roles, permissions);
            var refreshToken = await CreateRefreshTokenAsync(user.Id, ipAddress);

            await _db.SaveChangesAsync();

            await _audit.PublishAsync(
                AuditActions.Identity.AuthLoginSucceeded,
                entityType: nameof(User), entityId: user.Id.ToString(),
                after: new { user.Username, Roles = roles },
                category: AuditCategory.Security,
                classification: DataClassification.Credential);

            _logger.LogInformation("User '{Username}' logged in successfully.", user.Username);

            return new LoginResponse(accessToken, refreshToken.Token, expiry, user.Id, user.Username, user.AvatarUrl, roles, permissions);
        }

        public async Task<LoginResponse> RefreshTokenAsync(string refreshTokenStr, string? ipAddress = null)
        {
            var storedToken = await _db.RefreshTokens
                .Include(rt => rt.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                            .ThenInclude(r => r.RolePermissions)
                                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(rt => rt.Token == refreshTokenStr);

            if (storedToken == null || !storedToken.IsActive())
                throw new UnauthorizedException("Invalid or expired refresh token.");

            var user = storedToken.User;

            if (!user.IsActive)
                throw new UnauthorizedException("Account is deactivated.");

            // Rotate refresh token
            storedToken.Revoke();
            var newRefreshToken = await CreateRefreshTokenAsync(user.Id, ipAddress);

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
            var permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToArray();

            var (accessToken, expiry) = GenerateJwt(user, roles, permissions);
            await _db.SaveChangesAsync();

            return new LoginResponse(accessToken, newRefreshToken.Token, expiry, user.Id, user.Username, user.AvatarUrl, roles, permissions);
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var storedToken = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken);
            if (storedToken == null || !storedToken.IsActive())
                return; // idempotent — no error on already-revoked

            storedToken.Revoke();
            await _db.SaveChangesAsync();
            _logger.LogInformation("Refresh token revoked for user '{UserId}'.", storedToken.UserId);
        }

        public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            var user = await _db.Users.FindAsync(userId)
                ?? throw new NotFoundException("User", userId);

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                throw new UnauthorizedException("Current password is incorrect.");

            user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(request.NewPassword));

            // Revoke all existing refresh tokens for security
            var tokens = await _db.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();
            tokens.ForEach(t => t.Revoke());

            await _db.SaveChangesAsync();

            await _audit.PublishAsync(
                AuditActions.Identity.AuthPasswordChanged,
                entityType: nameof(User), entityId: userId.ToString(),
                after: new { Event = "PasswordChanged", UserId = userId },
                category: AuditCategory.Security,
                severity: AuditSeverity.Critical,
                classification: DataClassification.Credential);

            _logger.LogInformation("Password changed for user '{UserId}'.", userId);
        }

        // ── Private Helpers ────────────────────────────────────────────────────

        private (string token, DateTime expiry) GenerateJwt(User user, string[] roles, string[] permissions)
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

        private async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, string? ipAddress)
        {
            var expiryDays = _config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);
            var tokenBytes = RandomNumberGenerator.GetBytes(64);
            var tokenStr = Convert.ToBase64String(tokenBytes);

            var refreshToken = RefreshToken.Create(userId, tokenStr, expiryDays, ipAddress);
            _db.RefreshTokens.Add(refreshToken);
            return refreshToken;
        }
    }
}
