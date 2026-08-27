using Invoice.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Invoice.API.Infrastructure.Data.Configurations
{
    public class InvoiceConfiguration : IEntityTypeConfiguration<Domain.Entities.Invoice>
    {
        public void Configure(EntityTypeBuilder<Domain.Entities.Invoice> builder)
        {
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Amount).HasPrecision(18, 2);
            builder.Property(i => i.Version).IsConcurrencyToken();

            // Mỗi Order chỉ sinh tối đa 1 Invoice — filtered unique index (bỏ qua các dòng lịch sử
            // OrderId = NULL, tạo thủ công trước khi Invoice trở thành chứng từ phái sinh).
            builder.HasIndex(i => i.OrderId)
                .IsUnique()
                .HasFilter("[OrderId] IS NOT NULL");
        }
    }
}
