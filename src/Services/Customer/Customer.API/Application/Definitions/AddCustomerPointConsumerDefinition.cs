using MassTransit;
using Bizcore.BuildingBlocks.MassTransit;
using Customer.API.Application.Consumers;
using Bizcore.BuildingBlocks.Messaging;

namespace Customer.API.Application.Definitions
{
    public class AddCustomerPointConsumerDefinition : ConsumerDefinition<AddCustomerPointConsumer>
    {
        public AddCustomerPointConsumerDefinition()
        {
            // Map to the service-level queue to match Orchestration mapping
            EndpointName = QueueNames.CustomerService;
        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<AddCustomerPointConsumer> consumerConfigurator, IRegistrationContext context)
        {
            if (endpointConfigurator is IRabbitMqReceiveEndpointConfigurator rabbitMq)
            {
                rabbitMq.ApplyBusinessEndpointSettings();
            }
            
            consumerConfigurator.UseMessageRetry(r => r.Intervals(500, 1000, 5000));
        }
    }
}
