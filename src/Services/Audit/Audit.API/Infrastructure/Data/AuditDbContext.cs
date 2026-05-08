using Audit.API.Domain.Entities;
using Audit.API.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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

            // ── AuditEntries (Hot Storage) ────────────────────────────────────
            modelBuilder.Entity<AuditEntry>(e =>
            {
                e.ToTable("AuditEntries");
                e.HasKey(x => x.Id);

                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.ServiceName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Action).HasMaxLength(300).IsRequired();
                e.Property(x => x.AuditLevel)
                    .HasConversion<string>()
                    .HasMaxLength(30);
                e.Property(x => x.CorrelationId).HasMaxLength(100);
                e.Property(x => x.TraceId).HasMaxLength(64);
                e.Property(x => x.SpanId).HasMaxLength(32);
                e.Property(x => x.EntityName).HasMaxLength(200);
                e.Property(x => x.EntityId).HasMaxLength(200);
                e.Property(x => x.PerformedBy).HasMaxLength(200);
                e.Property(x => x.PerformedByName).HasMaxLength(200);
                e.Property(x => x.IpAddress).HasMaxLength(50);
                e.Property(x => x.UserAgent).HasMaxLength(500);
                e.Property(x => x.BeforeJson).HasColumnType("nvarchar(max)");
                e.Property(x => x.AfterJson).HasColumnType("nvarchar(max)");
                e.Property(x => x.Hash).HasMaxLength(64);
                e.Property(x => x.PreviousHash).HasMaxLength(64);
                e.Property(x => x.PerformedAt).IsRequired();

                // Performance indexes
                e.HasIndex(x => x.PerformedAt);
                e.HasIndex(x => x.ServiceName);
                e.HasIndex(x => x.EntityName);
                e.HasIndex(x => x.Action);
                e.HasIndex(x => x.AuditLevel);
                e.HasIndex(x => x.PerformedBy);
                e.HasIndex(x => x.CorrelationId);
            });

            // ── ArchiveEntries (Warm Storage) ─────────────────────────────────
            modelBuilder.Entity<ArchiveEntry>(e =>
            {
                e.ToTable("ArchiveEntries");
                e.HasKey(x => x.Id);

                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.ServiceName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Action).HasMaxLength(300).IsRequired();
                e.Property(x => x.AuditLevel)
                    .HasConversion<string>()
                    .HasMaxLength(30);
                e.Property(x => x.CorrelationId).HasMaxLength(100);
                e.Property(x => x.TraceId).HasMaxLength(64);
                e.Property(x => x.SpanId).HasMaxLength(32);
                e.Property(x => x.EntityName).HasMaxLength(200);
                e.Property(x => x.EntityId).HasMaxLength(200);
                e.Property(x => x.PerformedBy).HasMaxLength(200);
                e.Property(x => x.PerformedByName).HasMaxLength(200);
                e.Property(x => x.IpAddress).HasMaxLength(50);
                e.Property(x => x.UserAgent).HasMaxLength(500);
                e.Property(x => x.BeforeJson).HasColumnType("nvarchar(max)");
                e.Property(x => x.AfterJson).HasColumnType("nvarchar(max)");
                e.Property(x => x.Hash).HasMaxLength(64);
                e.Property(x => x.PreviousHash).HasMaxLength(64);

                e.HasIndex(x => x.PerformedAt);
                e.HasIndex(x => x.ServiceName);
                e.HasIndex(x => x.ArchivedAt);
            });

            // ── AuditHashChainHeads (Partitioned Hash Chain) ───────────────────
            modelBuilder.Entity<AuditHashChainHead>(e =>
            {
                e.ToTable("AuditHashChainHeads");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.PartitionKey, x.Sequence }).IsUnique();
            });
        }
    }
}
