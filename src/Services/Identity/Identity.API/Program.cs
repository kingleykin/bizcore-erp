using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using Bizcore.BuildingBlocks.MassTransit;
using Bizcore.BuildingBlocks.Middlewares;
using FluentValidation;
using FluentValidation.AspNetCore;
using Identity.API.Application.DTOs;
using Identity.API.Application.Services;
using Identity.API.Infrastructure.Data;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using StackExchange.Redis;
using System.Text;
using Bizcore.BuildingBlocks.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Serilog + Loki ────────────────────────────────────────────────────────
var lokiUrl = builder.Configuration.GetValue<string>("Loki:Url") ?? "http://loki:3100";
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "Identity.API")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.GrafanaLoki(lokiUrl,
        labels: new[]
        {
            new LokiLabel { Key = "service", Value = "identity-api" },
            new LokiLabel { Key = "environment", Value = builder.Environment.EnvironmentName }
        },
        propertiesAsLabels: new[] { "CorrelationId" })
    .CreateLogger();

builder.Host.UseSerilog();

// ── 2. Kestrel Hardening ─────────────────────────────────────────────────────
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024; // 5 MB
});

// ── 3. JWT Authentication ────────────────────────────────────────────────────
var secretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "bizcore-identity",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "bizcore-erp",
            ClockSkew = TimeSpan.Zero
        };
    });

// ── 4. Dynamic Authorization ─────────────────────────────────────────────────
// DynamicAuthorizationPolicyProvider tự động tạo policy từ permission name.
// Không cần khai báo tĩnh từng policy trong options.AddPolicy().
builder.Services.AddSingleton<IAuthorizationPolicyProvider, DynamicAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// ── 5. Redis Permission Cache ─────────────────────────────────────────────────
var redisConn = builder.Configuration.GetConnectionString("Redis")
             ?? builder.Configuration.GetValue<string>("Redis:ConnectionString")
             ?? "localhost:6379";

try
{
    var redis = await ConnectionMultiplexer.ConnectAsync(redisConn);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
    builder.Services.AddSingleton<IPermissionCache>(sp =>
        new RedisPermissionCache(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            sp.GetRequiredService<ILogger<RedisPermissionCache>>(),
            ttl: TimeSpan.FromMinutes(5)));
    Log.Information("Redis connected at {RedisConn}.", redisConn);
}
catch (Exception ex)
{
    Log.Warning(ex, "Redis connection failed. Permission cache will be disabled.");
    // App vẫn chạy được — chỉ không có cache
}

// ── 6. Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Đảm bảo DB tồn tại trước khi HealthChecks chạy
DatabaseExtensions.PreCreateDatabase(builder.Configuration.GetConnectionString("DefaultConnection")!);

// ── 7. MassTransit ────────────────────────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.UseCorrelationId(context);

        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        ));

        cfg.Host(builder.Configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
        {
            h.Username(builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
            h.Password(builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
        });
    });
});

// ── 8. MVC, Validation, API Versioning ───────────────────────────────────────
builder.Services.AddControllers(options =>
{
    options.Filters.Add<Identity.API.Filters.HttpExceptionFilter>();
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();

builder.Services.AddApiVersioning(options =>
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

// ── 9. Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BizCore Identity API",
        Version = "v1",
        Description = "Authentication, Authorization, User & Role Management Service"
    });

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

// ── 10. Health Checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "identity-db",
        tags: new[] { "db", "sql" });

// ── 11. Application Services ──────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddHttpContextAccessor();

// ── App Pipeline ──────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdPropagationMiddleware>();

app.UseSerilogRequestLogging();
app.UseHttpMetrics();

app.MapHealthChecks("/health");
app.MapMetrics();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BizCore Identity API v1");
    c.RoutePrefix = "swagger";
});

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Database Initialization & Seeding ────────────────────────────────────────
try
{
    await app.Services.MigrateDatabaseAsync<IdentityDbContext>();
    
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbSeeder.SeedAsync(db, seedLogger);
}
catch (Exception ex)
{
    using var scope = app.Services.CreateScope();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    seedLogger.LogError(ex, "Error occurred during database initialization/seeding.");
    throw;
}

app.Run();
