using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Orchestration.API.Application.Commands;
using Orchestration.API.Domain.Entities;
using Orchestration.API.Infrastructure.Data;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using Microsoft.Data.Sqlite;

namespace Bizcore.UnitTests;

public class OrchestrationCommandTests
{
    [Fact]
    public async Task RecordOrchestrationStepHandler_CreatesFlow_WhenNotExists()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        var invoiceId = Guid.NewGuid();

        using (var db = TestDbContextFactory.CreateOrchestrationDbContext(connection))
        {
            var handler = new RecordOrchestrationStepHandler(db);
            await handler.Handle(new RecordOrchestrationStepCommand(
                invoiceId, "InvoiceCreated", "InvoiceIndexed", new { }),
                CancellationToken.None);

            await db.SaveChangesAsync();

            var flow = await db.ProcessFlows.FirstOrDefaultAsync(f => f.InvoiceId == invoiceId);
            flow.Should().NotBeNull();
            flow!.Version.Should().Be(1);
        }
    }

    [Fact]
    public async Task RecordOrchestrationStepHandler_UpdatesFlow_Logic_Check()
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
            // Act
            var handler = new RecordOrchestrationStepHandler(db);
            await handler.Handle(new RecordOrchestrationStepCommand(
                invoiceId, "PaymentCompleted", "PaymentCaptured", new { }),
                CancellationToken.None);

            await db.SaveChangesAsync();

            // Assert
            var saved = await db.ProcessFlows.Include(f => f.Steps).FirstOrDefaultAsync(f => f.InvoiceId == invoiceId);
            saved.Should().NotBeNull();
            saved!.CurrentState.Should().Be("PaymentCaptured");
            saved.Version.Should().Be(2, "Version should be incremented after update");
            saved.Steps.Should().HaveCount(1);
        }
    }
}
