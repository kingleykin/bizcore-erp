using Admin.API.Application.DTOs;

namespace Admin.API.Application.Services
{
    public interface ISystemSettingsService
    {
        // Currency
        Task<IEnumerable<CurrencyResponse>> GetCurrenciesAsync(bool activeOnly = true);
        Task<CurrencyResponse?>             GetCurrencyByCodeAsync(string code);
        Task<CurrencyResponse>              CreateCurrencyAsync(CreateCurrencyRequest request);
        Task<bool>                          DeactivateCurrencyAsync(string code);

        // GlobalSetting
        Task<IEnumerable<GlobalSettingResponse>> GetAllSettingsAsync();
        Task<GlobalSettingResponse?>             GetSettingAsync(string key);
        Task<GlobalSettingResponse?>             UpdateSettingAsync(string key, UpdateSettingRequest request);

        // SystemCalendar
        Task<IEnumerable<SystemCalendarResponse>> GetCalendarAsync(int year);
        Task<SystemCalendarResponse>              UpsertCalendarDayAsync(UpsertCalendarRequest request);
    }
}
