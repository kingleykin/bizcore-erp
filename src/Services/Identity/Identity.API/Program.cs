using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Middlewares;
using FluentValidation;
using FluentValidation.AspNetCore;
using Identity.API.Application.DTOs;
using Identity.API.Application.Services;
using Identity.API.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using System.Text;

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

// ── 4. Authorization — Fine-grained Permission Policies ─────────────────────
builder.Services.AddAuthorization(options =>
{
    // Identity — Users
    options.AddPolicy("Identity.Users.View",              p => p.RequireClaim("permission", Permissions.Identity.Users.View));
    options.AddPolicy("Identity.Users.Create",            p => p.RequireClaim("permission", Permissions.Identity.Users.Create));
    options.AddPolicy("Identity.Users.Update",            p => p.RequireClaim("permission", Permissions.Identity.Users.Update));
    options.AddPolicy("Identity.Users.Delete",            p => p.RequireClaim("permission", Permissions.Identity.Users.Delete));
    options.AddPolicy("Identity.Users.ManageRoles",       p => p.RequireClaim("permission", Permissions.Identity.Users.ManageRoles));

    // Identity — Roles
    options.AddPolicy("Identity.Roles.View",              p => p.RequireClaim("permission", Permissions.Identity.Roles.View));
    options.AddPolicy("Identity.Roles.Create",            p => p.RequireClaim("permission", Permissions.Identity.Roles.Create));
    options.AddPolicy("Identity.Roles.Update",            p => p.RequireClaim("permission", Permissions.Identity.Roles.Update));
    options.AddPolicy("Identity.Roles.Delete",            p => p.RequireClaim("permission", Permissions.Identity.Roles.Delete));
    options.AddPolicy("Identity.Roles.ManagePermissions", p => p.RequireClaim("permission", Permissions.Identity.Roles.ManagePermissions));
});

// ── 5. Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── 6. MVC, Validation, API Versioning ───────────────────────────────────────
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

// ── 7. Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BizCore Identity API",
        Version = "v1",
        Description = "Authentication, Authorization, User & Role Management Service"
    });

    // JWT Security in Swagger
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

// ── 8. Health Checks ─────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "identity-db",
        tags: new[] { "db", "sql" });

// ── 9. Application Services (DI) ─────────────────────────────────────────────
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
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await DbSeeder.SeedAsync(db, seedLogger);
    }
    catch (Exception ex)
    {
        seedLogger.LogError(ex, "Error occurred during database seeding.");
        throw;
    }
}

app.Run();
