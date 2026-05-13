using MassTransit;
using Payment.API.Application.Consumers;

namespace Payment.API.Application.Definitions;

public class RejectPaymentConsumerDefinition : ConsumerDefinition<RejectPaymentConsumer>
{
    public RejectPaymentConsumerDefinition()
    {
        // Map to the service-level queue to match Orchestration mapping
        EndpointName = "payment-service";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<RejectPaymentConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Intervals(500, 1000, 5000));
    }
}
