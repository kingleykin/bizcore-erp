using Admin.API.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class SystemCalendarConfiguration : IEntityTypeConfiguration<SystemCalendar>
    {
        public void Configure(EntityTypeBuilder<SystemCalendar> builder)
        {
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => c.Date).IsUnique();
            
            builder.Property(c => c.HolidayName)
                .HasMaxLength(200);
        }
    }
}
