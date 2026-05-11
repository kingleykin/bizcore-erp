using MassTransit;
using Bizcore.BuildingBlocks.MassTransit;
using Invoice.API.Application.Consumers;
using Invoice.API.Infrastructure.Data;

namespace Invoice.API.Application.Definitions;

public class ValidateInvoiceConsumerDefinition : ConsumerDefinition<ValidateInvoiceCommandConsumer>
{
    public ValidateInvoiceConsumerDefinition()
    {
        // One service-level queue for all consumers in this service
        EndpointName = "invoice-service";
    }

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<ValidateInvoiceCommandConsumer> consumerConfigurator, IRegistrationContext context)
    {
        // Enable Inbox (Deduplication) for this consumer
        endpointConfigurator.UseEntityFrameworkOutbox<AppDbContext>(context);

        if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbitMq)
        {
            rabbitMq.ApplyBusinessEndpointSettings();
        }
        
        // Standard consumer settings
        consumerConfigurator.UseMessageRetry(r => r.Intervals(500, 1000, 5000));
    }
}
