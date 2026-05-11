namespace Admin.API.Domain.Entities.Settings
{
    /// <summary>
    /// Cấu hình hệ thống dạng Key-Value. SettingKey là Primary Key.
    /// Ví dụ: "System.DefaultTimezone" = "Asia/Ho_Chi_Minh"
    /// </summary>
    public class GlobalSetting
    {
        public string  SettingKey   { get; private set; } = null!;  // PK
        public string  SettingValue { get; private set; } = null!;
        public string? Description  { get; private set; }
        public bool    IsReadOnly   { get; private set; }           // System settings không được sửa qua API

        public DateTime UpdatedAt { get; private set; }

        private GlobalSetting() { }

        public static GlobalSetting Create(string key, string value, string? description = null, bool isReadOnly = false)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Setting key is required.", nameof(key));

            return new GlobalSetting
            {
                SettingKey   = key.Trim(),
                SettingValue = value,
                Description  = description?.Trim(),
                IsReadOnly   = isReadOnly,
                UpdatedAt    = DateTime.UtcNow
            };
        }

        public void UpdateValue(string newValue)
        {
            if (IsReadOnly)
                throw new InvalidOperationException($"Setting '{SettingKey}' is read-only and cannot be modified.");
            SettingValue = newValue;
            UpdatedAt    = DateTime.UtcNow;
        }
    }
}
