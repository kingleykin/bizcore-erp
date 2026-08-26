using MassTransit;
using Bizcore.BuildingBlocks.MassTransit;
using Order.API.Application.Consumers;

namespace Order.API.Application.Definitions;

/// <summary>
/// Bắt buộc phải có: Orchestration.API map IValidateOrderCommand tới exchange "order-service"
/// qua MapBusinessCommand(QueueNames.OrderService) — nếu không ép EndpointName về đúng tên này,
/// ValidateOrderCommandConsumer sẽ tự nhận endpoint riêng theo convention mặc định (khác tên),
/// khiến lệnh gửi vào exchange không ai lắng nghe — saga treo tới hết 60s rồi mới timeout.
/// </summary>
public class ValidateOrderConsumerDefinition : ConsumerDefinition<ValidateOrderCommandConsumer>
{
    public ValidateOrderConsumerDefinition()
    {
        // Map tới service-level queue để khớp với Orchestration mapping
        EndpointName = "order-service";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<ValidateOrderCommandConsumer> consumerConfigurator, IRegistrationContext context)
    {
        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbitMq)
        {
            rabbitMq.ApplyBusinessEndpointSettings();
        }

        consumerConfigurator.UseMessageRetry(r => r.Intervals(500, 1000, 5000));
    }
}
