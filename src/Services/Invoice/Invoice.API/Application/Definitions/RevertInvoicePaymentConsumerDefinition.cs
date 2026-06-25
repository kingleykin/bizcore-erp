using MassTransit;
using Invoice.API.Application.Consumers;

namespace Invoice.API.Application.Definitions;

public class RevertInvoicePaymentConsumerDefinition : ConsumerDefinition<RevertInvoicePaymentConsumer>
{
    public RevertInvoicePaymentConsumerDefinition()
    {
        // Map to the service-level queue to match Orchestration mapping
        EndpointName = "invoice-service";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<RevertInvoicePaymentConsumer> consumerConfigurator, IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Intervals(500, 1000, 5000));
    }
}
