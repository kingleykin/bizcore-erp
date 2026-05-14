using Bizcore.BuildingBlocks.Abstractions;

namespace Admin.API.Domain.Entities
{
    /// <summary>
    /// Refresh Token — Production-ready: revocable, expires, one-per-user-per-device strategy.
    /// </summary>
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; private set; }


        /// <summary>Token string (stored as hash in production, plain for demo).</summary>
        public string Token { get; private set; } = null!;

        public DateTime ExpiresAt { get; private set; }
        public bool IsRevoked { get; private set; }


        /// <summary>IP của client khi tạo token — dùng cho audit.</summary>
        public string? CreatedByIp { get; private set; }

        // Navigation
        public User User { get; private set; } = null!;

        private RefreshToken() { }

        public static RefreshToken Create(Guid userId, string token, int expiryDays, string? createdByIp = null)
        {
            return new RefreshToken
            {
                UserId = userId,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
                IsRevoked = false,
                CreatedByIp = createdByIp
            };

        }

        public void Revoke() => IsRevoked = true;

        public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;

        public bool IsActive() => !IsRevoked && !IsExpired();
    }
}
