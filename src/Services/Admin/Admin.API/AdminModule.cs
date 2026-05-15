using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;

using Bizcore.BuildingBlocks.Behaviors;
using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Messaging;
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
            services.AddScoped<IUnitOfWork, AdminUnitOfWork>();
            services.AddScoped<ITokenService, TokenService>();

            // 3. MediatR with Standard Behaviors
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            });

            // 4. Validation & Filters
            services.AddControllers(options =>
            {
                options.Filters.Add<Admin.API.Filters.HttpExceptionFilter>();
            });
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();

            // 5. MassTransit
            services.AddBizcoreMassTransit<AdminDbContext>(
                builder.Configuration,
                QueueNames.AdminService);
        }
    }
}
