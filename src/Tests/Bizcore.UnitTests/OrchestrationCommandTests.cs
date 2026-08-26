using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain.Entities;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace Bizcore.UnitTests;

public class OrchestrationCommandTests
{
    [Fact]
    public async Task OrchestrationStepRecorder_CreatesFlow_WhenNotExists()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        var invoiceId = Guid.NewGuid();

        using var db = TestDbContextFactory.CreateOrchestrationDbContext(connection);
        var recorder = new OrchestrationStepRecorder(db);

        await recorder.RecordAsync(
            invoiceId, "InvoiceCreated", "InvoiceIndexed", new { }, paymentId: null, CancellationToken.None);

        var flow = await db.ProcessFlows.FirstOrDefaultAsync(f => f.InvoiceId == invoiceId);
        flow.Should().NotBeNull();
        flow!.Version.Should().Be(1);
        flow.CurrentState.Should().Be("InvoiceIndexed");
    }

    [Fact]
    public async Task OrchestrationStepRecorder_UpdatesFlow_IncrementsVersion_AddsStep()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        var invoiceId = Guid.NewGuid();

        using (var db = TestDbContextFactory.CreateOrchestrationDbContext(connection))
        {
            // Seed
            var flow = ProcessFlow.Create(invoiceId);
            flow.MoveToState("Initial");
            db.ProcessFlows.Add(flow);
            await db.SaveChangesAsync();
            await db.Entry(flow).ReloadAsync();
            flow.Version.Should().Be(1);
        }

        using (var db = TestDbContextFactory.CreateOrchestrationDbContext(connection))
        {
            var recorder = new OrchestrationStepRecorder(db);
            await recorder.RecordAsync(
                invoiceId, "PaymentCompleted", "PaymentCaptured", new { }, paymentId: Guid.NewGuid(), CancellationToken.None);

            var saved = await db.ProcessFlows.Include(f => f.Steps).FirstOrDefaultAsync(f => f.InvoiceId == invoiceId);
            saved.Should().NotBeNull();
            saved!.CurrentState.Should().Be("PaymentCaptured");
            saved.Version.Should().Be(2, "Version should be incremented after update");
            saved.Steps.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task OrchestrationStepRecorder_PersistsWithoutExternalSaveChanges()
    {
        // Regression: khác với RecordOrchestrationStepHandler cũ (dựa vào TransactionBehavior gọi
        // SaveChangesAsync hộ — pattern này chỉ an toàn khi gọi qua MediatR ngoài consumer),
        // OrchestrationStepRecorder phải tự SaveChangesAsync vì được gọi TRỰC TIẾP từ MassTransit
        // consumer (không qua MediatR/ITransactionalCommand, tránh lỗi "connection already in a
        // transaction" do Transactional Inbox đã tự mở transaction sẵn).
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var db = TestDbContextFactory.CreateOrchestrationDbContext(connection);
        var invoiceId = Guid.NewGuid();

        var recorder = new OrchestrationStepRecorder(db);
        await recorder.RecordAsync(invoiceId, "InvoiceCreated", "InvoiceIndexed", new { }, null, CancellationToken.None);

        using var freshDb = TestDbContextFactory.CreateOrchestrationDbContext(connection);
        (await freshDb.ProcessFlows.AnyAsync(f => f.InvoiceId == invoiceId)).Should().BeTrue(
            "RecordAsync phải tự lưu DB, không phụ thuộc caller gọi SaveChangesAsync thêm");
    }
}
