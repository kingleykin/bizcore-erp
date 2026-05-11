using Admin.API.Domain.Entities;
using Admin.API.Domain.Entities.Organization;
using Admin.API.Domain.Entities.Settings;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Infrastructure.Data
{
    public class AdminDbContext : DbContext
    {
        public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

        // ── Identity & Authorization ──────────────────────────────────────────
        public DbSet<User>           Users           { get; set; }
        public DbSet<Role>           Roles           { get; set; }
        public DbSet<Permission>     Permissions     { get; set; }
        public DbSet<UserRole>       UserRoles       { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<RefreshToken>   RefreshTokens   { get; set; }
        public DbSet<NavigationMenu> NavigationMenus { get; set; }

        // ── Enterprise Organization ───────────────────────────────────────────
        public DbSet<LegalEntity>  LegalEntities { get; set; }
        public DbSet<Branch>       Branches      { get; set; }
        public DbSet<Department>   Departments   { get; set; }
        public DbSet<CostCenter>   CostCenters   { get; set; }

        // ── Global Settings ───────────────────────────────────────────────────
        public DbSet<Currency>       Currencies      { get; set; }
        public DbSet<SystemCalendar> SystemCalendars { get; set; }
        public DbSet<GlobalSetting>  GlobalSettings  { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ── User ─────────────────────────────────────────────────────────────
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(u => u.Id);
                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.Username).HasMaxLength(100).IsRequired();
                e.Property(u => u.Email).HasMaxLength(256).IsRequired();
                e.Property(u => u.PasswordHash).IsRequired();
            });

            // ── Role ─────────────────────────────────────────────────────────────
            modelBuilder.Entity<Role>(e =>
            {
                e.HasKey(r => r.Id);
                e.HasIndex(r => r.Name).IsUnique();
                e.Property(r => r.Name).HasMaxLength(100).IsRequired();
                e.Property(r => r.Description).HasMaxLength(500);
            });

            // ── Permission ────────────────────────────────────────────────────────
            modelBuilder.Entity<Permission>(e =>
            {
                e.HasKey(p => p.Id);
                e.HasIndex(p => p.Code).IsUnique();
                e.Property(p => p.Code).HasMaxLength(200).IsRequired();
                e.Property(p => p.Name).HasMaxLength(200).IsRequired();
                e.Property(p => p.Resource).HasMaxLength(100).IsRequired();
                e.Property(p => p.Scope).HasMaxLength(50).IsRequired();
                e.Property(p => p.Description).HasMaxLength(500);
            });

            // ── UserRole ──────────────────────────────────────────────────────────
            modelBuilder.Entity<UserRole>(e =>
            {
                e.HasKey(ur => new { ur.UserId, ur.RoleId });
                e.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
                e.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
            });

            // ── RolePermission ────────────────────────────────────────────────────
            modelBuilder.Entity<RolePermission>(e =>
            {
                e.HasKey(rp => new { rp.RoleId, rp.PermissionId });
                e.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
                e.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId);
            });

            // ── RefreshToken ──────────────────────────────────────────────────────
            modelBuilder.Entity<RefreshToken>(e =>
            {
                e.HasKey(rt => rt.Id);
                e.HasIndex(rt => rt.Token).IsUnique();
                e.HasOne(rt => rt.User).WithMany().HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // ── NavigationMenu ────────────────────────────────────────────────────
            modelBuilder.Entity<NavigationMenu>(e =>
            {
                e.HasKey(n => n.Id);
                e.Property(n => n.Name).HasMaxLength(100).IsRequired();
                e.Property(n => n.Route).HasMaxLength(300).IsRequired();
                e.Property(n => n.PermissionCode).HasMaxLength(200).IsRequired();
                e.Property(n => n.Icon).HasMaxLength(100);
                e.HasIndex(n => n.PermissionCode);
                e.HasIndex(n => n.SortOrder);
                e.HasOne(n => n.Parent)
                    .WithMany(n => n.Children)
                    .HasForeignKey(n => n.ParentId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
            });

            // ── LegalEntity ───────────────────────────────────────────────────────
            modelBuilder.Entity<LegalEntity>(e =>
            {
                e.HasKey(le => le.Id);
                e.HasIndex(le => le.Code).IsUnique();
                e.Property(le => le.Code).HasMaxLength(50).IsRequired();
                e.Property(le => le.Name).HasMaxLength(255).IsRequired();
                e.Property(le => le.TaxCode).HasMaxLength(50);
                e.Property(le => le.RegistrationNumber).HasMaxLength(50);
                e.Property(le => le.Address).HasMaxLength(500);
                e.Property(le => le.BaseCurrencyCode).HasMaxLength(3);
            });

            // ── Branch ───────────────────────────────────────────────────────────
            modelBuilder.Entity<Branch>(e =>
            {
                e.HasKey(b => b.Id);
                e.HasIndex(b => b.Code).IsUnique();
                e.Property(b => b.Code).HasMaxLength(50).IsRequired();
                e.Property(b => b.Name).HasMaxLength(255).IsRequired();
                e.Property(b => b.Address).HasMaxLength(500);
                e.HasOne(b => b.LegalEntity)
                    .WithMany(le => le.Branches)
                    .HasForeignKey(b => b.LegalEntityId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Department ────────────────────────────────────────────────────────
            modelBuilder.Entity<Department>(e =>
            {
                e.HasKey(d => d.Id);
                e.HasIndex(d => d.Code).IsUnique();
                e.Property(d => d.Code).HasMaxLength(50).IsRequired();
                e.Property(d => d.Name).HasMaxLength(255).IsRequired();
                e.HasOne(d => d.Branch)
                    .WithMany(b => b.Departments)
                    .HasForeignKey(d => d.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(d => d.Parent)
                    .WithMany(d => d.Children)
                    .HasForeignKey(d => d.ParentId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);
            });

            // ── CostCenter ────────────────────────────────────────────────────────
            modelBuilder.Entity<CostCenter>(e =>
            {
                e.HasKey(c => c.Id);
                e.HasIndex(c => c.Code).IsUnique();
                e.Property(c => c.Code).HasMaxLength(50).IsRequired();
                e.Property(c => c.Name).HasMaxLength(255).IsRequired();
                e.HasOne(c => c.LegalEntity)
                    .WithMany(le => le.CostCenters)
                    .HasForeignKey(c => c.LegalEntityId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Currency ──────────────────────────────────────────────────────────
            modelBuilder.Entity<Currency>(e =>
            {
                e.HasKey(c => c.Code);
                e.Property(c => c.Code).HasMaxLength(3).IsRequired();
                e.Property(c => c.Name).HasMaxLength(100).IsRequired();
                e.Property(c => c.Symbol).HasMaxLength(10).IsRequired();
            });

            // ── SystemCalendar ────────────────────────────────────────────────────
            modelBuilder.Entity<SystemCalendar>(e =>
            {
                e.HasKey(c => c.Date);
                e.Property(c => c.HolidayName).HasMaxLength(200);
            });

            // ── GlobalSetting ─────────────────────────────────────────────────────
            modelBuilder.Entity<GlobalSetting>(e =>
            {
                e.HasKey(s => s.SettingKey);
                e.Property(s => s.SettingKey).HasMaxLength(200).IsRequired();
                e.Property(s => s.SettingValue).IsRequired();
                e.Property(s => s.Description).HasMaxLength(500);
            });
        }
    }
}
