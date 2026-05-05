using Invoice.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Invoice.API.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Invoice.API.Domain.Entities.Invoice> Invoices { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Invoice.API.Domain.Entities.Invoice>().HasKey(i => i.Id);
            modelBuilder.Entity<Invoice.API.Domain.Entities.Invoice>().Property(i => i.Amount).HasPrecision(18, 2);
        }
    }
}
