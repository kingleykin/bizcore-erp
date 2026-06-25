using System;
using System.Threading;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Orchestration.API.Application.Commands;
using Orchestration.API.Application.Consumers;
using Orchestration.API.Domain.Entities;
using Xunit;

namespace Bizcore.UnitTests;

public class CustomerPointAddedOrchestrationConsumerTests
{
    private sealed class CustomerPointAddedEventFake : ICustomerPointAddedEvent
    {
        public CustomerPointAddedEventFake(Guid paymentId, Guid customerId, int points)
        {
            PaymentId = paymentId;
            CustomerId = customerId;
            Points = points;
        }

        public Guid PaymentId { get; }
        public Guid CustomerId { get; }
        public int Points { get; }
    }

    [Fact]
    public async Task Consume_WhenFlowExists_RecordsStep()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var db = TestDbContextFactory.CreateOrchestrationDbContext(connection);

        var invoiceId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Seed ProcessFlow
        var flow = ProcessFlow.Create(invoiceId);
        flow.MoveToState("PaymentCaptured", paymentId);
        db.ProcessFlows.Add(flow);
        await db.SaveChangesAsync();

        var mediatorMock = new Mock<IMediator>();
        var consumer = new CustomerPointAddedOrchestrationConsumer(
            db,
            mediatorMock.Object,
            NullLogger<CustomerPointAddedOrchestrationConsumer>.Instance);

        var consumeContext = new Mock<ConsumeContext<ICustomerPointAddedEvent>>();
        consumeContext
            .SetupGet(x => x.Message)
            .Returns(new CustomerPointAddedEventFake(paymentId, customerId, 15));

        // Act
        await consumer.Consume(consumeContext.Object);

        // Assert
        mediatorMock.Verify(m => m.Send(It.Is<RecordOrchestrationStepCommand>(cmd =>
            cmd.InvoiceId == invoiceId &&
            cmd.StepType == Orchestration.API.Domain.InvoicePaymentFlow.Steps.CustomerPointAddedObserved &&
            cmd.NewState == Orchestration.API.Domain.InvoicePaymentFlow.States.PaymentCaptured &&
            cmd.PaymentId == paymentId
        ), It.IsAny<CancellationToken>()), Times.Once);
    }
}
