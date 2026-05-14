using Audit.API.Domain.Entities;
using Audit.API.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Audit.API.Infrastructure.Data.Extensions;

namespace Audit.API.Infrastructure.Data
{
    public class AuditDbContext : DbContext
    {
        public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

        public DbSet<AuditEntry>   AuditEntries   { get; set; } = null!;
        public DbSet<ArchiveEntry> ArchiveEntries  { get; set; } = null!;
        public DbSet<AuditHashChainHead> AuditHashChainHeads { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
            modelBuilder.ConfigureMassTransitOutbox();
        }
    }
}
