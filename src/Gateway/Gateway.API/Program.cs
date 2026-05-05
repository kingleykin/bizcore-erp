using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Bizcore.BuildingBlocks.Middlewares;
using Yarp.ReverseProxy.Transforms;
using Microsoft.Extensions.Http.Resilience

var builder = WebApplication.CreateBuilder(args);

// 1. Logging with Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Hardening: Limit request size (10MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

// 3. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 4. Authentication & Authorization (Mock IdP Setup)
var secretKey = "BizcoreERPSecretKeyMustBeVeryLongAndSecure!!!"; // Demo secret
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User", "Admin"));
});

// 5. Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    options.AddPolicy("per-ip", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 50,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// 5. Health Checks
builder.Services.AddHealthChecks();

// 5. Resilience
builder.Services.AddResiliencePipeline("default", pipeline =>
{
    pipeline.AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = Polly.DelayBackoffType.Exponential
    });
    pipeline.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(15)
    });
});

// 6. YARP
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilder =>
    {
        transformBuilder.AddRequestTransform(context =>
        {
            var correlationId = context.HttpContext.Items["X-Correlation-ID"]?.ToString();
            if (!string.IsNullOrEmpty(correlationId))
            {
                context.ProxyRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
            }
            return ValueTask.CompletedTask;
        });
    });

var app = builder.Build();

// Order is important: Exception handler first, then Correlation ID
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/health");

using Bizcore.BuildingBlocks;

// 7. Mock Login Endpoint (Demonstration)
app.MapPost("/auth/login", (LoginRequest request) =>
{
    var claims = new List<Claim> { new Claim(ClaimTypes.Name, request.Username) };

    if (request.Username == "admin")
    {
        claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        claims.Add(new Claim("permission", Permissions.Invoice.View));
        claims.Add(new Claim("permission", Permissions.Invoice.Create));
        claims.Add(new Claim("permission", Permissions.Invoice.Update));
        claims.Add(new Claim("permission", Permissions.Report.View));
    }
    else if (request.Username == "user")
    {
        claims.Add(new Claim(ClaimTypes.Role, "User"));
        claims.Add(new Claim("permission", Permissions.Invoice.View));
        claims.Add(new Claim("permission", Permissions.Report.View));
    }
    else return Results.Unauthorized();

    var tokenHandler = new JwtSecurityTokenHandler();
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(claims),
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    return Results.Ok(new { Token = tokenHandler.WriteToken(token), Role = request.Username == "admin" ? "Admin" : "User" });
}).AllowAnonymous();

// 8. Security Headers & HTTPS
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; object-src 'none';";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 9. Map Reverse Proxy
app.MapReverseProxy().RequireRateLimiting("fixed").RequireAuthorization();

app.Run();

// Data models for Mock Auth
public record LoginRequest(string Username, string Password);
