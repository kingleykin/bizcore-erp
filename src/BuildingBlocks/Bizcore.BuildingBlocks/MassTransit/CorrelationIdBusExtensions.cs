using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcore.BuildingBlocks.MassTransit
{
    public static class CorrelationIdBusExtensions
    {
        /// <summary>
        /// Đăng ký CorrelationId publish + consume filters cho MassTransit.
        /// Gọi bên trong UsingRabbitMq() của mỗi service:
        ///
        ///   x.UsingRabbitMq((context, cfg) =>
        ///   {
        ///       cfg.UseCorrelationId(context);
        ///       ...
        ///   });
        /// </summary>
        public static void UseCorrelationId(
            this IBusFactoryConfigurator cfg,
            IBusRegistrationContext context)
        {
            var httpContextAccessor = context.GetRequiredService<IHttpContextAccessor>();

            // Publish filter: gắn CorrelationId từ HTTP context vào message header (Publish)
            cfg.UsePublishFilter(typeof(CorrelationIdPublishFilter<>), context);

            // Send filter: gắn CorrelationId từ HTTP context vào message header (Send/RequestClient)
            // IRequestClient.GetResponse() đi qua SendContext, không phải PublishContext
            cfg.UseSendFilter(typeof(CorrelationIdSendFilter<>), context);

            // Consume filter: đọc CorrelationId từ message header → push vào Serilog LogContext
            cfg.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);
        }
    }
}
