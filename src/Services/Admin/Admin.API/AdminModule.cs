using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;

namespace Admin.API
{
    public class AdminModule : IServiceModule
    {
        public void RegisterServices(IServiceCollection services, WebApplicationBuilder builder)
        {
            // 1. Database
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
            DatabaseExtensions.PreCreateDatabase(connStr);
            services.AddDbContext<AdminDbContext>(options => options.UseSqlServer(connStr));

            // 2. Application Services
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<ISystemSettingsService, SystemSettingsService>();

            // 3. Validation & Filters
            services.AddControllers(options =>
            {
                options.Filters.Add<Admin.API.Filters.HttpExceptionFilter>();
            });
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();

            // 4. MassTransit
            services.AddBizcoreMassTransit<AdminDbContext>(
                builder.Configuration,
                "admin-service"); // Admin usually doesn't have consumers yet, but good for outbox
        }
    }
}
