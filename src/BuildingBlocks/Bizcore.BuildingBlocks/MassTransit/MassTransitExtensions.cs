using MassTransit;
using Bizcore.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcore.BuildingBlocks.MassTransit;

public static class MassTransitExtensions
{
    public static void ApplyBusinessEndpointSettings(this IReceiveEndpointConfigurator configurator)
    {
        configurator.ConfigureConsumeTopology = true;
        
        if (configurator is IRabbitMqReceiveEndpointConfigurator rmq)
        {
            // Durable and AutoDelete are already true/false by default for RabbitMQ.
            // Explicitly setting them here in a callback can cause "modified after being used" errors.
            
            var queueName = rmq.InputAddress.AbsolutePath.Split('/').LastOrDefault();
            if (!string.IsNullOrEmpty(queueName))
            {
                rmq.SetQueueArgument("x-dead-letter-exchange", MessagingConstants.SharedDeadLetterExchange);
                rmq.SetQueueArgument("x-dead-letter-routing-key", $"{queueName}.error");
            }
        }
    }

    public static void ApplyRetryEndpointSettings(this IRabbitMqReceiveEndpointConfigurator rmq, int ttlMs = MessagingConstants.RetryTtlMs)
    {
        rmq.SetQueueArgument("x-message-ttl", ttlMs);
    }

    public static void MapBusinessCommand<T>(this IBusRegistrationConfigurator configurator, string serviceQueueName) where T : class
    {
        EndpointConvention.Map<T>(new Uri($"exchange:{serviceQueueName}"));
    }

    public static void AddBusinessOutbox<TDbContext>(this IBusRegistrationConfigurator x) where TDbContext : DbContext
    {
        x.AddEntityFrameworkOutbox<TDbContext>(o =>
        {
            o.UseSqlServer();
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromSeconds(1);
        });
    }

    /// <summary>
    /// Đăng ký MassTransit chuẩn cho Bizcore ERP (Kiến trúc tối ưu hóa).
    /// </summary>
    public static IServiceCollection AddBizcoreMassTransit<TDbContext>(
        this IServiceCollection services, 
        IConfiguration configuration,
        string serviceQueueName,
        Action<IBusRegistrationConfigurator>? extraConfig = null) 
        where TDbContext : DbContext
    {
        // Capture assembly TRƯỚC KHI vào lambda để tránh quét nhầm MassTransit assembly
        var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();

        services.AddMassTransit(x =>
        {
            // Sử dụng serviceQueueName làm TIỀN TỐ để phân tách các service trên cùng RabbitMQ broker
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(serviceQueueName, false));
            
            x.AddConsumers(callingAssembly);
            
            extraConfig?.Invoke(x);
            x.AddBusinessOutbox<TDbContext>();

            // Áp dụng Infrastructure (Outbox/Settings) cho TẤT CẢ endpoint tự động
            x.AddConfigureEndpointsCallback((context, name, cfg) =>
            {
                cfg.ApplyBusinessEndpointSettings();
                cfg.UseEntityFrameworkOutbox<TDbContext>(context);
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.UseCorrelationId(context);
                cfg.UseCulture(context);
                cfg.UseDelayedMessageScheduler();

                var host = configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
                var port = configuration.GetValue<ushort?>("RabbitMQ:Port") ?? 5672;

                cfg.Host(host, port, "/", h =>
                {
                    h.Username(configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
                    h.Password(configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
                });

                // Tự động cấu hình toàn bộ Consumers và Sagas
                // Giải pháp tối ưu: Không cấu hình ReceiveEndpoint thủ công để tránh xung đột
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
