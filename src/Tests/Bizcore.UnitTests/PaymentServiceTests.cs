using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Payment.API.Application.Services;
using PaymentEntity = Payment.API.Domain.Entities.Payment;
using PaymentInvoiceEntity = Payment.API.Domain.Entities.Invoice;

namespace Bizcore.UnitTests;

public class PaymentServiceTests
{
    [Fact]
    public async Task ProcessPaymentAsync_WhenIdempotencyKeyEmpty_ReturnsFalse()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreatePaymentDbContext(dbName);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var service = new PaymentService(context, publishMock.Object, cache);

        var payment = new PaymentEntity
        {
            InvoiceId = Guid.NewGuid(),
            Amount = 10_000m
        };

        var ok = await service.ProcessPaymentAsync(payment, "");

        ok.Should().BeFalse();
        context.Payments.Should().BeEmpty();
        cache.TryGetValue("", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenIdempotencyKeyExists_ReturnsTrue_WithoutPublishing()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreatePaymentDbContext(dbName);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var idempotencyKey = "idem-1";
        cache.Set(idempotencyKey, true);

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var service = new PaymentService(context, publishMock.Object, cache);

        var payment = new PaymentEntity
        {
            InvoiceId = Guid.NewGuid(),
            Amount = 25_000m
        };

        var ok = await service.ProcessPaymentAsync(payment, idempotencyKey);

        ok.Should().BeTrue();
        context.Payments.Should().BeEmpty();

        publishMock.Verify(
            p => p.Publish<IPaymentCompletedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenInvoiceMissing_ReturnsFalse()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreatePaymentDbContext(dbName);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var idempotencyKey = "idem-2";

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var service = new PaymentService(context, publishMock.Object, cache);

        var payment = new PaymentEntity
        {
            InvoiceId = Guid.NewGuid(),
            Amount = 99_000m
        };

        var ok = await service.ProcessPaymentAsync(payment, idempotencyKey);

        ok.Should().BeFalse();
        context.Payments.Should().BeEmpty();
        cache.TryGetValue(idempotencyKey, out _).Should().BeFalse();

        publishMock.Verify(
            p => p.Publish<IPaymentCompletedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenValid_StoresPayment_PublishesEvent_SetsCache()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreatePaymentDbContext(dbName);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var idempotencyKey = "idem-3";

        var invoiceId = Guid.NewGuid();
        context.Invoices.Add(new PaymentInvoiceEntity
        {
            Id = invoiceId,
            Status = InvoiceStatus.Pending
        });
        await context.SaveChangesAsync();

        var payment = new PaymentEntity
        {
            InvoiceId = invoiceId,
            Amount = 10_500m
        };

        var publishedValues = (object?)null;
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        publishMock
            .Setup(p => p.Publish<IPaymentCompletedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => publishedValues = values)
            .Returns(Task.CompletedTask);

        var service = new PaymentService(context, publishMock.Object, cache);

        var ok = await service.ProcessPaymentAsync(payment, idempotencyKey);

        ok.Should().BeTrue();

        var saved = context.Payments.Single(p => p.InvoiceId == invoiceId);
        saved.Id.Should().NotBe(Guid.Empty);
        saved.Amount.Should().Be(10_500m);
        saved.PaymentDate.Should().NotBe(default);
        saved.PaymentDate.Should().Be(payment.PaymentDate);
        saved.Status.Should().Be(Payment.API.Domain.Entities.PaymentStatus.Completed);

        cache.TryGetValue(idempotencyKey, out bool cachedValue).Should().BeTrue();
        cachedValue.Should().BeTrue();

        publishMock.Verify(
            p => p.Publish<IPaymentCompletedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);

        publishedValues.Should().NotBeNull();
        var publishedType = publishedValues!.GetType();
        publishedType.GetProperty("PaymentId")!.GetValue(publishedValues).Should().Be(saved.Id);
        publishedType.GetProperty("InvoiceId")!.GetValue(publishedValues).Should().Be(invoiceId);
        publishedType.GetProperty("Amount")!.GetValue(publishedValues).Should().Be(10_500m);
        publishedType.GetProperty("PaymentDate")!.GetValue(publishedValues).Should().Be(saved.PaymentDate);
    }
}

