using Bizcore.BuildingBlocks.Abstractions;

namespace Admin.API.Domain.Entities
{
    /// <summary>
    /// Thực thể User — chứa thông tin xác thực và trạng thái tài khoản.
    /// Theo chuẩn Production: Password hash BCrypt, account lockout, audit timestamps.
    /// </summary>
    public class User : BaseEntity
    {
        public string Username { get; private set; } = null!;
        public string PasswordHash { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        public string? AvatarUrl { get; private set; }
        public string PreferredLanguage { get; private set; } = "vi-VN";
        public bool IsActive { get; private set; }

        // Production-ready: Account Lockout
        public int FailedLoginAttempts { get; private set; }
        public DateTime? LockoutEnd { get; private set; }



        // Navigation properties
        public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

        private User() { } // EF Core constructor

        public static User Create(string username, string email, string passwordHash, string? avatarUrl = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required.", nameof(username));
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required.", nameof(email));
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("Password hash is required.", nameof(passwordHash));

            var user = new User
            {
                Username = username.Trim().ToLowerInvariant(),
                Email = email.Trim().ToLowerInvariant(),
                PasswordHash = passwordHash,
                AvatarUrl = avatarUrl,
                IsActive = true,
                FailedLoginAttempts = 0,
                LockoutEnd = null
            };

            return user;

        }

        public void UpdateAvatar(string? avatarUrl)
        {
            AvatarUrl = avatarUrl;
            UpdateTimestamp();

        }

        public void UpdateProfile(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required.", nameof(email));

            Email = email.Trim().ToLowerInvariant();
            UpdateTimestamp();

        }

        public void SetPreferredLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                throw new ArgumentException("Language code is required.", nameof(languageCode));

            PreferredLanguage = languageCode;
            UpdateTimestamp();

        }

        public void UpdatePassword(string newPasswordHash)
        {
            if (string.IsNullOrWhiteSpace(newPasswordHash))
                throw new ArgumentException("Password hash is required.", nameof(newPasswordHash));

            PasswordHash = newPasswordHash;
            UpdateTimestamp();

        }

        public void Deactivate()
        {
            IsActive = false;
            UpdateTimestamp();

        }

        public void Activate()
        {
            IsActive = true;
            UpdateTimestamp();

        }

        /// <summary>
        /// Ghi nhận login thất bại. Sau 5 lần sẽ khóa tài khoản 15 phút.
        /// </summary>
        public void RecordFailedLogin(int maxAttempts = 5, int lockoutMinutes = 15)
        {
            FailedLoginAttempts++;
            if (FailedLoginAttempts >= maxAttempts)
            {
                LockoutEnd = DateTime.UtcNow.AddMinutes(lockoutMinutes);
            }
            UpdateTimestamp();

        }

        /// <summary>
        /// Reset trạng thái sau khi login thành công.
        /// </summary>
        public void ResetFailedLogins()
        {
            FailedLoginAttempts = 0;
            LockoutEnd = null;
            UpdateTimestamp();

        }

        public bool IsLockedOut() =>
            LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;
    }
}
