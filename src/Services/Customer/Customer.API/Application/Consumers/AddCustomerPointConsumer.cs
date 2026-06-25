using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Application.Commands;
using MassTransit;
using MediatR;

namespace Customer.API.Application.Consumers
{
    public class AddCustomerPointConsumer : IConsumer<IAddCustomerPointCommand>
    {
        private readonly IMediator _mediator;

        public AddCustomerPointConsumer(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task Consume(ConsumeContext<IAddCustomerPointCommand> context)
        {


            await _mediator.Send(new AddCustomerPointCommand(
                context.Message.PaymentId,
                context.Message.CustomerId,
                context.Message.Amount
            ), context.CancellationToken);
        }
    }
}
