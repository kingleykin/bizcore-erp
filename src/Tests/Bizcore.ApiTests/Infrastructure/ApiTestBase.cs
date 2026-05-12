using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Testcontainers.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Bizcore.ApiTests.Infrastructure;

public abstract class ApiTestBase<TEntryPoint> : IAsyncLifetime where TEntryPoint : class
{
    protected readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Password123!")
        .Build();

    protected readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7.0")
        .Build();

    protected readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .Build();

    protected WebApplicationFactory<TEntryPoint> _factory = default!;
    protected HttpClient _client = default!;

    public virtual async Task InitializeAsync()
    {
        await Task.WhenAll(_dbContainer.StartAsync(), _redisContainer.StartAsync(), _rabbitMqContainer.StartAsync());

        // Ensure connection string has TrustServerCertificate=True for SQL Server containers
        var dbConnectionString = _dbContainer.GetConnectionString();
        if (!dbConnectionString.Contains("TrustServerCertificate", StringComparison.OrdinalIgnoreCase))
        {
            dbConnectionString = $"{dbConnectionString.TrimEnd(';')};TrustServerCertificate=True;Encrypt=False;";
        }

        var redisConnectionString = _redisContainer.GetConnectionString();
        
        // Use environment variables as they are picked up more reliably by WebApplicationBuilder 
        // during early service registration in Program.cs
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", dbConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", redisConnectionString);
        Environment.SetEnvironmentVariable("RabbitMQ__Host", _rabbitMqContainer.Hostname);
        Environment.SetEnvironmentVariable("RabbitMQ__Port", _rabbitMqContainer.GetMappedPublicPort(5672).ToString());
        Environment.SetEnvironmentVariable("Jwt__SecretKey", "SuperSecretKeyForTestingPurposes123!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "bizcore-admin");
        Environment.SetEnvironmentVariable("Jwt__Audience", "bizcore-erp");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");
        Environment.SetEnvironmentVariable("OTEL_SDK_DISABLED", "true");

        _factory = new WebApplicationFactory<TEntryPoint>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    // Still add to configuration for components that read it later
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = dbConnectionString,
                        ["ConnectionStrings:Redis"] = redisConnectionString,
                        ["RabbitMQ:Host"] = _rabbitMqContainer.Hostname,
                        ["RabbitMQ:Port"] = _rabbitMqContainer.GetMappedPublicPort(5672).ToString(),
                        ["Jwt:SecretKey"] = "SuperSecretKeyForTestingPurposes123!",
                        ["Jwt:Issuer"] = "bizcore-admin",
                        ["Jwt:Audience"] = "bizcore-erp",
                        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
                        ["OTEL_SDK_DISABLED"] = "true"
                    });
                });

                builder.ConfigureLogging(logging => {
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Warning);
                });
            });

        _client = _factory.CreateClient();
    }

    public virtual async Task DisposeAsync()
    {
        if (_client != null) _client.Dispose();
        if (_factory != null) await _factory.DisposeAsync();
        
        await Task.WhenAll(
            _dbContainer.DisposeAsync().AsTask(), 
            _redisContainer.DisposeAsync().AsTask(), 
            _rabbitMqContainer.DisposeAsync().AsTask()
        );
    }
}
