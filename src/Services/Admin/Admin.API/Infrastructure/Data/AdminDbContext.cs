using Admin.API.Domain.Entities;
using Admin.API.Domain.Entities.Organization;
using Admin.API.Domain.Entities.Settings;
using Admin.API.Infrastructure.Data.Extensions;
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
            base.OnModelCreating(modelBuilder);

            // Apply all configurations from the current assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);

            // Configure MassTransit Outbox
            modelBuilder.ConfigureMassTransitOutbox();
        }
    }
}
