using System.ComponentModel.DataAnnotations;

namespace Admin.API.Application.DTOs
{
    // ── Currency ───────────────────────────────────────────────────────────────

    public record CurrencyResponse(
        string Code,
        string Name,
        string Symbol,
        int    DecimalPlaces,
        bool   IsActive
    );

    public record CreateCurrencyRequest
    {
        [Required, MaxLength(3)]
        public string Code { get; init; } = null!;

        [Required, MaxLength(100)]
        public string Name { get; init; } = null!;

        [Required, MaxLength(10)]
        public string Symbol { get; init; } = null!;

        [Range(0, 4)]
        public int DecimalPlaces { get; init; } = 2;
    }

    // ── GlobalSetting ──────────────────────────────────────────────────────────

    public record GlobalSettingResponse(
        string  SettingKey,
        string  SettingValue,
        string? Description,
        bool    IsReadOnly,
        DateTime UpdatedAt
    );

    public record UpdateSettingRequest
    {
        [Required]
        public string SettingValue { get; init; } = null!;
    }

    // ── SystemCalendar ─────────────────────────────────────────────────────────

    public record SystemCalendarResponse(
        DateTime Date,
        bool     IsWorkingDay,
        string?  HolidayName
    );

    public record UpsertCalendarRequest
    {
        [Required]
        public DateTime Date { get; init; }

        public bool     IsWorkingDay { get; init; } = true;
        public string?  HolidayName  { get; init; }
    }
}
