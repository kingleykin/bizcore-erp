using MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcore.BuildingBlocks.MassTransit;

public static class MassTransitExtensions
{
    /// <summary>
    /// Applies standard enterprise settings to a Business Critical receive endpoint.
    /// - Durable = true (Messages are persisted to disk)
    /// - AutoDelete = false (Queue stays even if no consumers)
    /// - NO TTL (Business data must NOT expire automatically)
    /// - Shared DLX (bizcore.dlx) with routing key {queue-name}.error
    /// </summary>
    public static void ApplyBusinessEndpointSettings(this IRabbitMqReceiveEndpointConfigurator configurator)
    {
        configurator.Durable = true;
        configurator.AutoDelete = false;
        
        // REMOVED Global TTL to prevent data loss in ERP/Accounting flows
        
        // Configure Shared Dead Letter Exchange
        var queueName = configurator.InputAddress.AbsolutePath.Split('/').LastOrDefault();
        if (!string.IsNullOrEmpty(queueName))
        {
            configurator.SetQueueArgument("x-dead-letter-exchange", MessagingConstants.SharedDeadLetterExchange);
            configurator.SetQueueArgument("x-dead-letter-routing-key", $"{queueName}.error");
        }
    }

    /// <summary>
    /// Applies settings for a Retry or Transient queue that DOES require TTL.
    /// </summary>
    public static void ApplyRetryEndpointSettings(this IRabbitMqReceiveEndpointConfigurator configurator, int ttlMs = MessagingConstants.RetryTtlMs)
    {
        configurator.Durable = true;
        configurator.AutoDelete = false;
        configurator.SetQueueArgument("x-message-ttl", ttlMs);
    }

    /// <summary>
    /// Maps a command to a Service-Level Exchange. 
    /// This ensures that the Sender only declares an Exchange, 
    /// while the Receiver owns the Queue and its complex configuration (DLX, TTL, etc).
    /// </summary>
    public static void MapBusinessCommand<T>(this IBusRegistrationConfigurator configurator, string serviceQueueName) where T : class
    {
        // We map to "exchange:name" instead of "queue:name" 
        // to decouple Sender topology from Receiver queue arguments.
        EndpointConvention.Map<T>(new Uri($"exchange:{serviceQueueName}"));
    }

    /// <summary>
    /// Adds production-grade Entity Framework Outbox & Inbox.
    /// - Outbox: Guarantees atomicity between DB changes and message publishing.
    /// - Inbox: Guarantees idempotency (deduplication) of incoming messages.
    /// </summary>
    public static void AddBusinessOutbox<TDbContext>(this IBusRegistrationConfigurator x) where TDbContext : DbContext
    {
        x.AddEntityFrameworkOutbox<TDbContext>(o =>
        {
            o.UseSqlServer();
            o.UseBusOutbox(); // Atomicity for Send/Publish from HTTP/Service layer
            
            // Tune for higher throughput in ERP flows
            o.QueryDelay = TimeSpan.FromSeconds(1);

            // Note: Inbox/Outbox cleanup is enabled by default in MT 8
        });
    }

    /// <summary>
    /// Automates MassTransit registration with standard ERP settings.
    /// - Convention-based consumer registration.
    /// - Service-level receive endpoint with Outbox.
    /// - Automatic endpoint configuration via ConsumerDefinitions.
    /// </summary>
    public static IServiceCollection AddBizcoreMassTransit<TDbContext>(
        this IServiceCollection services, 
        IConfiguration configuration,
        string serviceQueueName,
        Action<IBusRegistrationConfigurator>? extraConfig = null) 
        where TDbContext : DbContext
    {
        services.AddMassTransit(x =>
        {
            // 1. Convention-based consumer registration (from the calling assembly)
            x.AddConsumers(System.Reflection.Assembly.GetCallingAssembly());
            
            // Allow extra consumers/sagas
            extraConfig?.Invoke(x);

            x.AddBusinessOutbox<TDbContext>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.ConfigureBusinessBus(context);
                cfg.Host(configuration.GetValue<string>("RabbitMQ:Host"), "/", h =>
                {
                    h.Username(configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
                    h.Password(configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
                });

                // 2. Centralized Service Endpoint with Outbox
                cfg.ReceiveEndpoint(serviceQueueName, e =>
                {
                    e.ApplyBusinessEndpointSettings();
                    e.ConfigureConsumers(context); // Automatically configures all consumers for this endpoint
                    e.UseEntityFrameworkOutbox<TDbContext>(context);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>
    /// Configures global business topology and observability.
    /// </summary>
    public static void ConfigureBusinessBus(this IRabbitMqBusFactoryConfigurator cfg, IBusRegistrationContext context)
    {
        // Standardize Correlation and Tracing
        cfg.UseCorrelationId(context);
        
        // Use Quartz for all scheduling/delayed messages
        cfg.UsePublishMessageScheduler();
        
        // Ensure ALL consumers on this bus use the Outbox/Inbox
        // Note: This applies it globally to all ReceiveEndpoints on this host
        // cfg.UseEntityFrameworkOutbox<TDbContext>(context); 
        // ^ This needs a generic type, so we usually call it in the specific Program.cs or via a helper.
    }
}
