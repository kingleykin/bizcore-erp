using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orchestration.API.Application.Sagas;
using Orchestration.API.Domain.Entities;
using Xunit;
using Bizcore.BuildingBlocks.Messaging;
using Bizcore.BuildingBlocks.MassTransit;

namespace Bizcore.UnitTests
{
    public class PaymentSagaTests
    {
        [Fact]
        public async Task PaymentInitiated_ShouldStartSaga_AndSendValidateInvoiceCommand()
        {
            // Arrange
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(x =>
                {
                    x.MapBusinessCommand<IValidateInvoiceCommand>(QueueNames.InvoiceService);
                    x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
                    x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);
                    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var paymentId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var amount = 1500m;

            // Act
            await harness.Bus.Publish<IPaymentInitiatedEvent>(new
            {
                PaymentId = paymentId,
                InvoiceId = invoiceId,
                Amount = amount,
                IdempotencyKey = "test-key",
                InitiatedAt = DateTime.UtcNow
            });

            // Assert
            // 1. Check if Saga was started
            var sagaHarness = harness.GetSagaStateMachineHarness<PaymentSaga, PaymentSagaState>();
            (await sagaHarness.Consumed.Any<IPaymentInitiatedEvent>()).Should().BeTrue();
            (await sagaHarness.Created.Any(x => x.CorrelationId == paymentId)).Should().BeTrue();

            // 2. Check if state transitioned to Validating
            var instance = sagaHarness.Created.Contains(paymentId);
            instance.Should().NotBeNull();
            instance!.CurrentState.Should().Be("Validating");

            // 3. Check if ValidateInvoiceCommand was sent
            (await harness.Sent.Any<IValidateInvoiceCommand>()).Should().BeTrue();
            var sentCommand = harness.Sent.Select<IValidateInvoiceCommand>().First();
            sentCommand.Context.Message.PaymentId.Should().Be(paymentId);
            sentCommand.Context.Message.InvoiceId.Should().Be(invoiceId);
            sentCommand.Context.Message.Amount.Should().Be(amount);
        }

        [Fact]
        public async Task InvoiceValidated_ShouldConfirmPayment_AndFinalizeSaga()
        {
            // Arrange
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(x =>
                {
                    x.MapBusinessCommand<IValidateInvoiceCommand>(QueueNames.InvoiceService);
                    x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
                    x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);
                    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var paymentId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();

            // 1. Start Saga
            await harness.Bus.Publish<IPaymentInitiatedEvent>(new
            {
                PaymentId = paymentId,
                InvoiceId = invoiceId,
                Amount = 1000m,
                IdempotencyKey = "test-key",
                InitiatedAt = DateTime.UtcNow
            });

            var sagaHarness = harness.GetSagaStateMachineHarness<PaymentSaga, PaymentSagaState>();
            (await sagaHarness.Created.Any(x => x.CorrelationId == paymentId)).Should().BeTrue();

            // 2. Simulate Invoice Validated
            await harness.Bus.Publish<IInvoiceValidatedEvent>(new
            {
                PaymentId = paymentId,
                InvoiceId = invoiceId,
                ValidatedAt = DateTime.UtcNow
            });

            // Assert
            (await sagaHarness.Consumed.Any<IInvoiceValidatedEvent>()).Should().BeTrue();
            
            // 3. Check if ConfirmPaymentCommand was sent
            (await harness.Sent.Any<IConfirmPaymentCommand>()).Should().BeTrue();
            
            // 4. Check if Saga instance is finalized (Completed)
            var instance = sagaHarness.Sagas.Contains(paymentId);
            instance.Should().NotBeNull();
            instance!.CurrentState.Should().Be("Confirmed");
        }

        [Fact]
        public async Task InvoiceValidationFailed_ShouldRejectPayment()
        {
            // Arrange
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(x =>
                {
                    x.MapBusinessCommand<IValidateInvoiceCommand>(QueueNames.InvoiceService);
                    x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
                    x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);
                    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var paymentId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();

            // 1. Start Saga
            await harness.Bus.Publish<IPaymentInitiatedEvent>(new
            {
                PaymentId = paymentId,
                InvoiceId = invoiceId,
                Amount = 1000m,
                IdempotencyKey = "test-key",
                InitiatedAt = DateTime.UtcNow
            });

            var sagaHarness = harness.GetSagaStateMachineHarness<PaymentSaga, PaymentSagaState>();

            // 2. Simulate Invoice Validation Failed
            await harness.Bus.Publish<IInvoiceValidationFailedEvent>(new
            {
                PaymentId = paymentId,
                InvoiceId = invoiceId,
                Reason = "Insufficient stock",
                FailedAt = DateTime.UtcNow
            });

            // Assert
            (await sagaHarness.Consumed.Any<IInvoiceValidationFailedEvent>()).Should().BeTrue();
            
            // 3. Check if RejectPaymentCommand was sent
            (await harness.Sent.Any<IRejectPaymentCommand>()).Should().BeTrue();
            var sent = harness.Sent.Select<IRejectPaymentCommand>().First();
            sent.Context.Message.Reason.Should().Be("Insufficient stock");

            // 4. State should be Rejected
            var instance = sagaHarness.Sagas.Contains(paymentId);
            instance!.CurrentState.Should().Be("Rejected");
        }

        [Fact]
        public async Task ValidationTimeout_ShouldRejectPayment_AndTransitionToTimedOut()
        {
            // Arrange
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(x =>
                {
                    x.MapBusinessCommand<IValidateInvoiceCommand>(QueueNames.InvoiceService);
                    x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
                    x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);
                    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var paymentId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();

            // 1. Start Saga
            await harness.Bus.Publish<IPaymentInitiatedEvent>(new
            {
                PaymentId = paymentId,
                InvoiceId = invoiceId,
                Amount = 1000m,
                IdempotencyKey = "timeout-test",
                InitiatedAt = DateTime.UtcNow
            });

            var sagaHarness = harness.GetSagaStateMachineHarness<PaymentSaga, PaymentSagaState>();
            (await sagaHarness.Created.Any(x => x.CorrelationId == paymentId)).Should().BeTrue();

            // 2. Assert: Verify that a message was sent (this would be the schedule request)
            (await harness.Sent.Any()).Should().BeTrue();
            
            // 3. To trigger the actual timeout logic without waiting 60s, 
            // we simulate the arrival of the timeout message
            await harness.Bus.Publish<PaymentValidationTimeout>(new { PaymentId = paymentId });

            // 4. Assert: Check if RejectPaymentCommand was sent due to timeout
            (await harness.Sent.Any<IRejectPaymentCommand>()).Should().BeTrue();
            
            // 5. State should be TimedOut
            var instance = sagaHarness.Sagas.Contains(paymentId);
            instance!.CurrentState.Should().Be("TimedOut");
        }

        [Fact]
        public async Task PaymentInitiated_WithOrderId_ShouldStartSaga_AndSendValidateOrderCommand_NotInvoiceCommand()
        {
            // Regression guard: PaymentSaga dùng chung 1 saga cho cả Invoice lẫn Order — nếu
            // OrderId có giá trị thì phải rẽ nhánh gửi IValidateOrderCommand, TUYỆT ĐỐI không
            // được gửi IValidateInvoiceCommand (sẽ crash Invoice.API vì thiếu InvoiceId thật).
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(x =>
                {
                    x.MapBusinessCommand<IValidateInvoiceCommand>(QueueNames.InvoiceService);
                    x.MapBusinessCommand<IValidateOrderCommand>(QueueNames.OrderService);
                    x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
                    x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);
                    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var paymentId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var amount = 250m;

            await harness.Bus.Publish<IPaymentInitiatedEvent>(new
            {
                PaymentId = paymentId,
                OrderId = orderId,
                Amount = amount,
                IdempotencyKey = "order-test-key",
                InitiatedAt = DateTime.UtcNow
            });

            var sagaHarness = harness.GetSagaStateMachineHarness<PaymentSaga, PaymentSagaState>();
            (await sagaHarness.Created.Any(x => x.CorrelationId == paymentId)).Should().BeTrue();

            var instance = sagaHarness.Created.Contains(paymentId);
            instance!.CurrentState.Should().Be("Validating");
            instance.OrderId.Should().Be(orderId);
            instance.InvoiceId.Should().BeNull();

            (await harness.Sent.Any<IValidateOrderCommand>()).Should().BeTrue();
            var sentCommand = harness.Sent.Select<IValidateOrderCommand>().First();
            sentCommand.Context.Message.PaymentId.Should().Be(paymentId);
            sentCommand.Context.Message.OrderId.Should().Be(orderId);
            sentCommand.Context.Message.Amount.Should().Be(amount);

            (await harness.Sent.Any<IValidateInvoiceCommand>()).Should().BeFalse(
                "payment cho Order tuyệt đối không được gửi lệnh validate Invoice");
        }

        [Fact]
        public async Task OrderValidated_ShouldConfirmPayment_WithOrderIdOnCommand_AndFinalizeSaga()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(x =>
                {
                    x.MapBusinessCommand<IValidateOrderCommand>(QueueNames.OrderService);
                    x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
                    x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);
                    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var paymentId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish<IPaymentInitiatedEvent>(new
            {
                PaymentId = paymentId,
                OrderId = orderId,
                Amount = 900m,
                IdempotencyKey = "order-confirm-key",
                InitiatedAt = DateTime.UtcNow
            });

            var sagaHarness = harness.GetSagaStateMachineHarness<PaymentSaga, PaymentSagaState>();
            (await sagaHarness.Created.Any(x => x.CorrelationId == paymentId)).Should().BeTrue();

            await harness.Bus.Publish<IOrderValidatedEvent>(new
            {
                PaymentId = paymentId,
                OrderId = orderId,
                ValidatedAt = DateTime.UtcNow
            });

            (await sagaHarness.Consumed.Any<IOrderValidatedEvent>()).Should().BeTrue();

            (await harness.Sent.Any<IConfirmPaymentCommand>()).Should().BeTrue();
            var sent = harness.Sent.Select<IConfirmPaymentCommand>().First();
            sent.Context.Message.PaymentId.Should().Be(paymentId);
            sent.Context.Message.OrderId.Should().Be(orderId);
            sent.Context.Message.InvoiceId.Should().BeNull();

            var instance = sagaHarness.Sagas.Contains(paymentId);
            instance!.CurrentState.Should().Be("Confirmed");
        }

        [Fact]
        public async Task OrderValidationFailed_ShouldRejectPayment_WithOrderIdOnCommand()
        {
            await using var provider = new ServiceCollection()
                .AddMassTransitTestHarness(x =>
                {
                    x.MapBusinessCommand<IValidateOrderCommand>(QueueNames.OrderService);
                    x.MapBusinessCommand<IConfirmPaymentCommand>(QueueNames.PaymentService);
                    x.MapBusinessCommand<IRejectPaymentCommand>(QueueNames.PaymentService);
                    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>();
                })
                .BuildServiceProvider(true);

            var harness = provider.GetRequiredService<ITestHarness>();
            await harness.Start();

            var paymentId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish<IPaymentInitiatedEvent>(new
            {
                PaymentId = paymentId,
                OrderId = orderId,
                Amount = 900m,
                IdempotencyKey = "order-reject-key",
                InitiatedAt = DateTime.UtcNow
            });

            var sagaHarness = harness.GetSagaStateMachineHarness<PaymentSaga, PaymentSagaState>();

            await harness.Bus.Publish<IOrderValidationFailedEvent>(new
            {
                PaymentId = paymentId,
                OrderId = orderId,
                Reason = "Order already confirmed/paid.",
                FailedAt = DateTime.UtcNow
            });

            (await sagaHarness.Consumed.Any<IOrderValidationFailedEvent>()).Should().BeTrue();

            (await harness.Sent.Any<IRejectPaymentCommand>()).Should().BeTrue();
            var sent = harness.Sent.Select<IRejectPaymentCommand>().First();
            sent.Context.Message.PaymentId.Should().Be(paymentId);
            sent.Context.Message.OrderId.Should().Be(orderId);
            sent.Context.Message.Reason.Should().Be("Order already confirmed/paid.");

            var instance = sagaHarness.Sagas.Contains(paymentId);
            instance!.CurrentState.Should().Be("Rejected");
        }
    }
}
