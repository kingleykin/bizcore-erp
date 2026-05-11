namespace Admin.API.Domain.Entities.Settings
{
    /// <summary>
    /// Lịch làm việc hệ thống. Date là Primary Key.
    /// Được dùng để xác định ngày làm việc và ngày nghỉ lễ.
    /// </summary>
    public class SystemCalendar
    {
        public DateTime Date         { get; private set; }  // PK
        public bool     IsWorkingDay { get; private set; }
        public string?  HolidayName  { get; private set; }  // null nếu là ngày thường

        private SystemCalendar() { }

        public static SystemCalendar Create(DateTime date, bool isWorkingDay, string? holidayName = null)
        {
            return new SystemCalendar
            {
                Date         = date.Date,  // normalize to date-only
                IsWorkingDay = isWorkingDay,
                HolidayName  = holidayName?.Trim()
            };
        }

        public void Update(bool isWorkingDay, string? holidayName)
        {
            IsWorkingDay = isWorkingDay;
            HolidayName  = holidayName?.Trim();
        }
    }
}
