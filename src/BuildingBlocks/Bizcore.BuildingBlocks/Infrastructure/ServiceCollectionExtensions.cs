using Asp.Versioning;
using Bizcore.BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Text;

namespace Bizcore.BuildingBlocks.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBizcoreAuth(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. JWT Authentication
            var secretKey = configuration["Jwt:SecretKey"] 
                ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
            var key = Encoding.ASCII.GetBytes(secretKey);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidIssuer = configuration["Jwt:Issuer"] ?? "bizcore-admin",
                        ValidateAudience = true,
                        ValidAudience = configuration["Jwt:Audience"] ?? "bizcore-erp",
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) &&
                                (path.StartsWithSegments("/hubs")))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });


            // 2. Redis + Dynamic Authorization
            var redisConnection = configuration.GetConnectionString("Redis") ?? "redis:6379";
            services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnection));
            services.AddScoped<IPermissionCache, RedisPermissionCache>();
            services.AddSingleton<IAuthorizationPolicyProvider, DynamicAuthorizationPolicyProvider>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddAuthorization();

            return services;
        }

        public static IServiceCollection AddBizcoreSwagger(this IServiceCollection services, string title, string description)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1", Description = description });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization. Enter 'Bearer {token}'",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });
            return services;
        }

        public static IServiceCollection AddBizcoreVersioning(this IServiceCollection services)
        {
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });
            return services;
        }

        public static IServiceCollection AddBizcoreInfrastructure(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddControllers();
            services.AddHealthChecks();
            return services;
        }
    }
}
