using Admin.API.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admin.API.Infrastructure.Data.Configurations
{
    public class GlobalSettingConfiguration : IEntityTypeConfiguration<GlobalSetting>
    {
        public void Configure(EntityTypeBuilder<GlobalSetting> builder)
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.SettingKey).IsUnique();
            
            builder.Property(s => s.SettingKey)
                .HasMaxLength(200)
                .IsRequired();
                
            builder.Property(s => s.SettingValue)
                .IsRequired();
                
            builder.Property(s => s.Description)
                .HasMaxLength(500);
        }
    }
}
