using Identity.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Infrastructure.Data
{
    public class IdentityDbContext : DbContext
    {
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- User ---
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(u => u.Id);
                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.Username).HasMaxLength(100).IsRequired();
                e.Property(u => u.Email).HasMaxLength(256).IsRequired();
                e.Property(u => u.PasswordHash).IsRequired();
            });

            // --- Role ---
            modelBuilder.Entity<Role>(e =>
            {
                e.HasKey(r => r.Id);
                e.HasIndex(r => r.Name).IsUnique();
                e.Property(r => r.Name).HasMaxLength(100).IsRequired();
                e.Property(r => r.Description).HasMaxLength(500);
            });

            // --- Permission ---
            modelBuilder.Entity<Permission>(e =>
            {
                e.HasKey(p => p.Id);
                e.HasIndex(p => p.Action).IsUnique();
                e.Property(p => p.Action).HasMaxLength(200).IsRequired();
                e.Property(p => p.Description).HasMaxLength(500);
            });

            // --- UserRole (composite PK) ---
            modelBuilder.Entity<UserRole>(e =>
            {
                e.HasKey(ur => new { ur.UserId, ur.RoleId });
                e.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
                e.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
            });

            // --- RolePermission (composite PK) ---
            modelBuilder.Entity<RolePermission>(e =>
            {
                e.HasKey(rp => new { rp.RoleId, rp.PermissionId });
                e.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
                e.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId);
            });

            // --- RefreshToken ---
            modelBuilder.Entity<RefreshToken>(e =>
            {
                e.HasKey(rt => rt.Id);
                e.HasIndex(rt => rt.Token).IsUnique();
                e.HasOne(rt => rt.User).WithMany().HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // --- AuditLog ---
            modelBuilder.Entity<AuditLog>(e =>
            {
                e.HasKey(al => al.Id);
                e.Property(al => al.Action).HasMaxLength(200).IsRequired();
                e.Property(al => al.TargetEntityType).HasMaxLength(100);
                e.Property(al => al.TargetEntityId).HasMaxLength(100);
                e.Property(al => al.IpAddress).HasMaxLength(50);
                // No FK constraint on ActorUserId — log must survive user deletion
            });
        }
    }
}
