using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using Invoice.API.Application.Consumers;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Microsoft.Data.Sqlite;

namespace Bizcore.UnitTests;

/// <summary>
/// Tests cho ApplyPaymentToInvoiceConsumer — thay thế Invoice.PaymentCompletedConsumer
/// đã bị xóa sau khi chuyển sang Request-Reply pattern.
/// </summary>
public class ApplyPaymentToInvoiceConsumerTests
{
    private sealed class ApplyPaymentRequestFake : IApplyPaymentToInvoiceRequest
    {
        public Guid PaymentId { get; init; }
        public Guid InvoiceId { get; init; }
        public decimal Amount { get; init; }
    }

    [Fact]
    public async Task Consume_WhenInvoiceValid_UpdatesToPaid_RespondsSuccess()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(connection);
        var invoice = InvoiceEntity.Create("Alice", 1_000m);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        IApplyPaymentToInvoiceResponse? response = null;
        var ctx = new Mock<ConsumeContext<IApplyPaymentToInvoiceRequest>>();
        ctx.SetupGet(x => x.Message)
           .Returns(new ApplyPaymentRequestFake { PaymentId = Guid.NewGuid(), InvoiceId = invoice.Id, Amount = 1_000m });
        ctx.Setup(x => x.RespondAsync<IApplyPaymentToInvoiceResponse>(It.IsAny<object>()))
           .Callback<object>(r => response = Mock.Of<IApplyPaymentToInvoiceResponse>(m =>
               m.Success == (bool)r.GetType().GetProperty("Success")!.GetValue(r)! &&
               m.ErrorReason == (string?)r.GetType().GetProperty("ErrorReason")!.GetValue(r)))
           .Returns(Task.CompletedTask);

        var consumer = new ApplyPaymentToInvoiceConsumer(context, NullLogger<ApplyPaymentToInvoiceConsumer>.Instance);
        await consumer.Consume(ctx.Object);

        context.Invoices.Single(i => i.Id == invoice.Id).Status.Should().Be(InvoiceStatus.Paid);
        response!.Success.Should().BeTrue();
        response.ErrorReason.Should().BeNull();
    }

    [Fact]
    public async Task Consume_WhenInvoiceMissing_RespondsFailure()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(connection);

        IApplyPaymentToInvoiceResponse? response = null;
        var ctx = new Mock<ConsumeContext<IApplyPaymentToInvoiceRequest>>();
        ctx.SetupGet(x => x.Message)
           .Returns(new ApplyPaymentRequestFake { PaymentId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(), Amount = 500m });
        ctx.Setup(x => x.RespondAsync<IApplyPaymentToInvoiceResponse>(It.IsAny<object>()))
           .Callback<object>(r => response = Mock.Of<IApplyPaymentToInvoiceResponse>(m =>
               m.Success == (bool)r.GetType().GetProperty("Success")!.GetValue(r)! &&
               m.ErrorReason == (string?)r.GetType().GetProperty("ErrorReason")!.GetValue(r)))
           .Returns(Task.CompletedTask);

        var consumer = new ApplyPaymentToInvoiceConsumer(context, NullLogger<ApplyPaymentToInvoiceConsumer>.Instance);
        await consumer.Consume(ctx.Object);

        response!.Success.Should().BeFalse();
        response.ErrorReason.Should().Contain("not found");
        context.Invoices.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_WhenAmountMismatch_RespondsFailure_LeavesInvoicePending()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(connection);
        var invoice = InvoiceEntity.Create("Bob", 500m);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        IApplyPaymentToInvoiceResponse? response = null;
        var ctx = new Mock<ConsumeContext<IApplyPaymentToInvoiceRequest>>();
        ctx.SetupGet(x => x.Message)
           .Returns(new ApplyPaymentRequestFake { PaymentId = Guid.NewGuid(), InvoiceId = invoice.Id, Amount = 999m });
        ctx.Setup(x => x.RespondAsync<IApplyPaymentToInvoiceResponse>(It.IsAny<object>()))
           .Callback<object>(r => response = Mock.Of<IApplyPaymentToInvoiceResponse>(m =>
               m.Success == (bool)r.GetType().GetProperty("Success")!.GetValue(r)! &&
               m.ErrorReason == (string?)r.GetType().GetProperty("ErrorReason")!.GetValue(r)))
           .Returns(Task.CompletedTask);

        var consumer = new ApplyPaymentToInvoiceConsumer(context, NullLogger<ApplyPaymentToInvoiceConsumer>.Instance);
        await consumer.Consume(ctx.Object);

        context.Invoices.Single(i => i.Id == invoice.Id).Status.Should().Be(InvoiceStatus.Pending);
        response!.Success.Should().BeFalse();
        response.ErrorReason.Should().Contain("mismatch");
    }

    [Fact]
    public async Task Consume_WhenInvoiceAlreadyPaid_RespondsFailure()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(connection);
        var invoice = InvoiceEntity.Create("Carol", 300m);
        invoice.UpdateStatus(InvoiceStatus.Paid);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        IApplyPaymentToInvoiceResponse? response = null;
        var ctx = new Mock<ConsumeContext<IApplyPaymentToInvoiceRequest>>();
        ctx.SetupGet(x => x.Message)
           .Returns(new ApplyPaymentRequestFake { PaymentId = Guid.NewGuid(), InvoiceId = invoice.Id, Amount = 300m });
        ctx.Setup(x => x.RespondAsync<IApplyPaymentToInvoiceResponse>(It.IsAny<object>()))
           .Callback<object>(r => response = Mock.Of<IApplyPaymentToInvoiceResponse>(m =>
               m.Success == (bool)r.GetType().GetProperty("Success")!.GetValue(r)! &&
               m.ErrorReason == (string?)r.GetType().GetProperty("ErrorReason")!.GetValue(r)))
           .Returns(Task.CompletedTask);

        var consumer = new ApplyPaymentToInvoiceConsumer(context, NullLogger<ApplyPaymentToInvoiceConsumer>.Instance);
        await consumer.Consume(ctx.Object);

        response!.Success.Should().BeFalse();
        response.ErrorReason.Should().Contain("already paid");
    }

    [Fact]
    public async Task Consume_WhenInvoiceCancelled_RespondsFailure()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(connection);
        var invoice = InvoiceEntity.Create("Dave", 150m);
        invoice.UpdateStatus(InvoiceStatus.Cancelled);
        await context.Invoices.AddAsync(invoice);
        await context.SaveChangesAsync();

        IApplyPaymentToInvoiceResponse? response = null;
        var ctx = new Mock<ConsumeContext<IApplyPaymentToInvoiceRequest>>();
        ctx.SetupGet(x => x.Message)
           .Returns(new ApplyPaymentRequestFake { PaymentId = Guid.NewGuid(), InvoiceId = invoice.Id, Amount = 150m });
        ctx.Setup(x => x.RespondAsync<IApplyPaymentToInvoiceResponse>(It.IsAny<object>()))
           .Callback<object>(r => response = Mock.Of<IApplyPaymentToInvoiceResponse>(m =>
               m.Success == (bool)r.GetType().GetProperty("Success")!.GetValue(r)! &&
               m.ErrorReason == (string?)r.GetType().GetProperty("ErrorReason")!.GetValue(r)))
           .Returns(Task.CompletedTask);

        var consumer = new ApplyPaymentToInvoiceConsumer(context, NullLogger<ApplyPaymentToInvoiceConsumer>.Instance);
        await consumer.Consume(ctx.Object);

        context.Invoices.Single(i => i.Id == invoice.Id).Status.Should().Be(InvoiceStatus.Cancelled);
        response!.Success.Should().BeFalse();
        response.ErrorReason.Should().Contain("cancelled");
    }
}
