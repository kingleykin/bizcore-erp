using Bizcore.BuildingBlocks.Infrastructure;
using Bizcore.BuildingBlocks.Middlewares;
using Gateway.API;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Host Extensions ────────────────────────────────────────────────────────
builder.Host.AddBizcoreLogging("Gateway.API");

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

// ── 2. Service Registrations (Centralized + Module) ──────────────────────────
builder.Services.AddBizcoreTelemetry("Gateway.API");
builder.Services.AddBizcoreInfrastructure();
builder.Services.AddBizcoreAuth(builder.Configuration);

// Load Service Specific Module
builder.Services.AddBizcoreModule<GatewayModule>(builder);

// ── 3. YARP Registration (Moved here for better debugging) ────────────────────
var reverseProxyConfig = builder.Configuration.GetSection("ReverseProxy");
if (!reverseProxyConfig.Exists())
{
    throw new InvalidOperationException("Phần cấu hình 'ReverseProxy' không tìm thấy trong appsettings.json");
}

builder.Services.AddReverseProxy()
    .LoadFromConfig(reverseProxyConfig);

// ── 4. App Pipeline ───────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/health");
app.UseCors("AllowFrontend");

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Content-Security-Policy"] = 
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "connect-src 'self' http://localhost:5001 http://localhost:3000; object-src 'none';";
    await next();
});

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseSerilogRequestLogging();
app.UseHttpMetrics();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Map Reverse Proxy
app.MapReverseProxy().RequireRateLimiting("fixed");
app.MapMetrics();

app.Run();

namespace Gateway.API
{
    public partial class Program { }
}