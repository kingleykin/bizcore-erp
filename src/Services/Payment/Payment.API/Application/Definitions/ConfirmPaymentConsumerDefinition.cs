using MassTransit;
using Payment.API.Application.Consumers;

namespace Payment.API.Application.Definitions;

public class ConfirmPaymentConsumerDefinition : ConsumerDefinition<ConfirmPaymentConsumer>
{
    public ConfirmPaymentConsumerDefinition()
    {
        // Map to the service-level queue to match Orchestration mapping
        EndpointName = "payment-service";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<ConfirmPaymentConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Intervals(500, 1000, 5000));
    }
}
