using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain;

namespace Bizcore.UnitTests;

public class ProcessOrchestrationServiceTests
{
    private sealed class InvoiceCreatedFake : Bizcore.BuildingBlocks.Contracts.IInvoiceCreatedEvent
    {
        public InvoiceCreatedFake(Guid id, string customerName, decimal amount, DateTime createdAt)
        {
            Id = id;
            CustomerName = customerName;
            Amount = amount;
            CreatedAt = createdAt;
        }

        public Guid Id { get; }
        public string CustomerName { get; }
        public decimal Amount { get; }
        public DateTime CreatedAt { get; }
    }

    private sealed class PaymentCompletedFake : Bizcore.BuildingBlocks.Contracts.IPaymentCompletedEvent
    {
        public PaymentCompletedFake(Guid paymentId, Guid invoiceId, decimal amount, DateTime paymentDate)
        {
            PaymentId = paymentId;
            InvoiceId = invoiceId;
            Amount = amount;
            PaymentDate = paymentDate;
        }

        public Guid PaymentId { get; }
        public Guid InvoiceId { get; }
        public decimal Amount { get; }
        public DateTime PaymentDate { get; }
    }

    [Fact]
    public async Task RecordInvoiceCreatedAsync_CreatesFlow_AndStep()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = TestDbContextFactory.CreateOrchestrationDbContext(dbName);
        var svc = new ProcessOrchestrationService(db);

        var id = Guid.NewGuid();
        await svc.RecordInvoiceCreatedAsync(new InvoiceCreatedFake(id, "A", 100m, DateTime.UtcNow));

        var flow = db.ProcessFlows.Single(f => f.InvoiceId == id);
        flow.CurrentState.Should().Be(InvoicePaymentFlow.States.InvoiceIndexed);
        db.FlowSteps.Count(s => s.ProcessFlowId == flow.Id).Should().Be(1);
    }

    [Fact]
    public async Task RecordPaymentCompletedAsync_AppendsStep_AndSetsState()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var db = TestDbContextFactory.CreateOrchestrationDbContext(dbName);
        var svc = new ProcessOrchestrationService(db);

        var invoiceId = Guid.NewGuid();
        await svc.RecordInvoiceCreatedAsync(new InvoiceCreatedFake(invoiceId, "B", 200m, DateTime.UtcNow));

        var paymentId = Guid.NewGuid();
        await svc.RecordPaymentCompletedAsync(new PaymentCompletedFake(paymentId, invoiceId, 200m, DateTime.UtcNow));

        var flow = db.ProcessFlows.Single(f => f.InvoiceId == invoiceId);
        flow.CurrentState.Should().Be(InvoicePaymentFlow.States.PaymentCaptured);
        flow.LastPaymentId.Should().Be(paymentId);
        db.FlowSteps.Count(s => s.ProcessFlowId == flow.Id).Should().Be(2);
    }
}
