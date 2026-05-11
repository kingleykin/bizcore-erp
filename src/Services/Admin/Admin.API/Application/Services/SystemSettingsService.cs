using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities.Settings;
using Admin.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Services
{
    public class SystemSettingsService : ISystemSettingsService
    {
        private readonly AdminDbContext _db;
        private readonly ILogger<SystemSettingsService> _logger;

        public SystemSettingsService(AdminDbContext db, ILogger<SystemSettingsService> logger)
        {
            _db     = db;
            _logger = logger;
        }

        // ── Currency ───────────────────────────────────────────────────────────

        public async Task<IEnumerable<CurrencyResponse>> GetCurrenciesAsync(bool activeOnly = true)
        {
            var query = _db.Currencies.AsQueryable();
            if (activeOnly) query = query.Where(c => c.IsActive);
            var list = await query.OrderBy(c => c.Code).ToListAsync();
            return list.Select(MapCurrency);
        }

        public async Task<CurrencyResponse?> GetCurrencyByCodeAsync(string code)
        {
            var currency = await _db.Currencies.FindAsync(code.ToUpperInvariant());
            return currency is null ? null : MapCurrency(currency);
        }

        public async Task<CurrencyResponse> CreateCurrencyAsync(CreateCurrencyRequest request)
        {
            var code = request.Code.ToUpperInvariant();
            if (await _db.Currencies.AnyAsync(c => c.Code == code))
                throw new InvalidOperationException($"Currency '{code}' already exists.");

            var currency = Currency.Create(code, request.Name, request.Symbol, request.DecimalPlaces);
            _db.Currencies.Add(currency);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created Currency {Code}.", currency.Code);
            return MapCurrency(currency);
        }

        public async Task<bool> DeactivateCurrencyAsync(string code)
        {
            var currency = await _db.Currencies.FindAsync(code.ToUpperInvariant());
            if (currency is null) return false;
            currency.Deactivate();
            await _db.SaveChangesAsync();
            return true;
        }

        // ── GlobalSetting ──────────────────────────────────────────────────────

        public async Task<IEnumerable<GlobalSettingResponse>> GetAllSettingsAsync()
        {
            var settings = await _db.GlobalSettings.OrderBy(s => s.SettingKey).ToListAsync();
            return settings.Select(MapSetting);
        }

        public async Task<GlobalSettingResponse?> GetSettingAsync(string key)
        {
            var setting = await _db.GlobalSettings.FindAsync(key);
            return setting is null ? null : MapSetting(setting);
        }

        public async Task<GlobalSettingResponse?> UpdateSettingAsync(string key, UpdateSettingRequest request)
        {
            var setting = await _db.GlobalSettings.FindAsync(key);
            if (setting is null) return null;

            setting.UpdateValue(request.SettingValue);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Updated GlobalSetting '{Key}'.", key);
            return MapSetting(setting);
        }

        // ── SystemCalendar ─────────────────────────────────────────────────────

        public async Task<IEnumerable<SystemCalendarResponse>> GetCalendarAsync(int year)
        {
            var start = new DateTime(year, 1, 1);
            var end   = new DateTime(year, 12, 31);
            var days  = await _db.SystemCalendars
                .Where(c => c.Date >= start && c.Date <= end)
                .OrderBy(c => c.Date)
                .ToListAsync();
            return days.Select(MapCalendar);
        }

        public async Task<SystemCalendarResponse> UpsertCalendarDayAsync(UpsertCalendarRequest request)
        {
            var date    = request.Date.Date;
            var existing = await _db.SystemCalendars.FindAsync(date);

            if (existing is not null)
            {
                existing.Update(request.IsWorkingDay, request.HolidayName);
            }
            else
            {
                var day = SystemCalendar.Create(date, request.IsWorkingDay, request.HolidayName);
                _db.SystemCalendars.Add(day);
                existing = day;
            }

            await _db.SaveChangesAsync();
            return MapCalendar(existing);
        }

        // ── Mapping helpers ────────────────────────────────────────────────────

        private static CurrencyResponse MapCurrency(Currency c) =>
            new(c.Code, c.Name, c.Symbol, c.DecimalPlaces, c.IsActive);

        private static GlobalSettingResponse MapSetting(GlobalSetting s) =>
            new(s.SettingKey, s.SettingValue, s.Description, s.IsReadOnly, s.UpdatedAt);

        private static SystemCalendarResponse MapCalendar(SystemCalendar c) =>
            new(c.Date, c.IsWorkingDay, c.HolidayName);
    }
}
