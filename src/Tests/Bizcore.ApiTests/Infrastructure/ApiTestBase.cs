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

        _factory = new WebApplicationFactory<TEntryPoint>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                        ["ConnectionStrings:Redis"] = _redisContainer.GetConnectionString(),
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
                    logging.ClearProviders();
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
