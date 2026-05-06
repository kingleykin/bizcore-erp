using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Payment.API.Application.Services;
using PaymentEntity = Payment.API.Domain.Entities.Payment;
using PaymentInvoiceEntity = Payment.API.Domain.Entities.Invoice;

namespace Bizcore.UnitTests;

public class PaymentServiceTests
{
    // -------------------------------------------------------------------------
    // Helper: tạo mock IRequestClient trả về response tuỳ ý
    // -------------------------------------------------------------------------
    private static Mock<IRequestClient<IApplyPaymentToInvoiceRequest>> BuildClientMock(
        bool success, string? errorReason = null)
    {
        var responseMock = new Mock<Response<IApplyPaymentToInvoiceResponse>>();
        var msgMock = new Mock<IApplyPaymentToInvoiceResponse>();
        msgMock.Setup(m => m.Success).Returns(success);
        msgMock.Setup(m => m.ErrorReason).Returns(errorReason);
        responseMock.Setup(r => r.Message).Returns(msgMock.Object);

        var clientMock = new Mock<IRequestClient<IApplyPaymentToInvoiceRequest>>();
        clientMock
            .Setup(c => c.GetResponse<IApplyPaymentToInvoiceResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<RequestTimeout>()))
            .ReturnsAsync(responseMock.Object);

        return clientMock;
    }

    private static PaymentService BuildService(
        Payment.API.Infrastructure.Data.AppDbContext context,
        IMemoryCache cache,
        Mock<IRequestClient<IApplyPaymentToInvoiceRequest>>? clientMock = null,
        Mock<IPublishEndpoint>? publishMock = null)
    {
        clientMock ??= BuildClientMock(true);
        publishMock ??= new Mock<IPublishEndpoint>();
        publishMock.Setup(p => p.Publish<IPaymentCompletedEvent>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new PaymentService(context, clientMock.Object, publishMock.Object, cache, NullLogger<PaymentService>.Instance);
    }

    // =========================================================================
    // 1. Idempotency key rỗng → từ chối ngay
    // =========================================================================

    [Fact]
    public async Task ProcessPaymentAsync_WhenIdempotencyKeyEmpty_ReturnsFalse()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = BuildService(context, cache);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = Guid.NewGuid(), Amount = 10_000m }, "");

        result.Success.Should().BeFalse();
        context.Payments.Should().BeEmpty();
    }

    // =========================================================================
    // 2. Idempotency key đã tồn tại → trả về success, không gọi Invoice
    // =========================================================================

    [Fact]
    public async Task ProcessPaymentAsync_WhenIdempotencyKeyExists_ReturnsTrue_WithoutCallingInvoice()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var cache = new MemoryCache(new MemoryCacheOptions());
        var idempotencyKey = "idem-duplicate";
        cache.Set(idempotencyKey, true);

        var clientMock = BuildClientMock(true);
        var service = BuildService(context, cache, clientMock);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = Guid.NewGuid(), Amount = 25_000m }, idempotencyKey);

        result.Success.Should().BeTrue();
        context.Payments.Should().BeEmpty();

        // Invoice service không được gọi khi idempotency key đã tồn tại
        clientMock.Verify(
            c => c.GetResponse<IApplyPaymentToInvoiceResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<RequestTimeout>()),
            Times.Never);
    }

    // =========================================================================
    // 3. Invoice không tồn tại trong read model → từ chối, không gọi Invoice
    // =========================================================================

    [Fact]
    public async Task ProcessPaymentAsync_WhenInvoiceMissingInReadModel_ReturnsFalse()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var cache = new MemoryCache(new MemoryCacheOptions());

        var clientMock = BuildClientMock(true);
        var service = BuildService(context, cache, clientMock);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = Guid.NewGuid(), Amount = 99_000m }, "idem-no-invoice");

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().NotBeNullOrEmpty();
        context.Payments.Should().BeEmpty();

        clientMock.Verify(
            c => c.GetResponse<IApplyPaymentToInvoiceResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<RequestTimeout>()),
            Times.Never);
    }

    // =========================================================================
    // 4. Invoice service từ chối → không lưu payment, trả về lỗi rõ ràng
    // =========================================================================

    [Fact]
    public async Task ProcessPaymentAsync_WhenInvoiceServiceRejects_ReturnsFalse_DoesNotSavePayment()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var cache = new MemoryCache(new MemoryCacheOptions());

        var invoiceId = Guid.NewGuid();
        context.Invoices.Add(new PaymentInvoiceEntity { Id = invoiceId, Status = InvoiceStatus.Pending });
        await context.SaveChangesAsync();

        var clientMock = BuildClientMock(success: false, errorReason: "Invoice is already paid.");
        var service = BuildService(context, cache, clientMock);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = invoiceId, Amount = 5_000m }, "idem-rejected");

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be("Invoice is already paid.");
        context.Payments.Should().BeEmpty("payment không được lưu khi Invoice từ chối");
        cache.TryGetValue("idem-rejected", out _).Should().BeFalse("idempotency key không được set khi thất bại");
    }

    // =========================================================================
    // 5. Happy path: Invoice xác nhận → lưu payment, set cache, publish event
    // =========================================================================

    [Fact]
    public async Task ProcessPaymentAsync_WhenInvoiceServiceAccepts_SavesPayment_SetsCache_PublishesEvent()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var cache = new MemoryCache(new MemoryCacheOptions());
        var idempotencyKey = "idem-success";

        var invoiceId = Guid.NewGuid();
        context.Invoices.Add(new PaymentInvoiceEntity { Id = invoiceId, Status = InvoiceStatus.Pending });
        await context.SaveChangesAsync();

        var clientMock = BuildClientMock(success: true);
        var publishMock = new Mock<IPublishEndpoint>();
        publishMock.Setup(p => p.Publish<IPaymentCompletedEvent>(
            It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(context, cache, clientMock, publishMock);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = invoiceId, Amount = 10_500m }, idempotencyKey);

        result.Success.Should().BeTrue();
        result.ErrorReason.Should().BeNull();

        var saved = context.Payments.Single(p => p.InvoiceId == invoiceId);
        saved.Id.Should().NotBe(Guid.Empty);
        saved.Amount.Should().Be(10_500m);
        saved.Status.Should().Be(Payment.API.Domain.Entities.PaymentStatus.Completed);
        saved.PaymentDate.Should().NotBe(default);

        cache.TryGetValue(idempotencyKey, out bool cachedValue).Should().BeTrue();
        cachedValue.Should().BeTrue();

        // IPaymentCompletedEvent phải được publish để Report và Orchestration cập nhật
        publishMock.Verify(
            p => p.Publish<IPaymentCompletedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenInvoiceServiceRejects_DoesNotPublishEvent()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var cache = new MemoryCache(new MemoryCacheOptions());

        var invoiceId = Guid.NewGuid();
        context.Invoices.Add(new PaymentInvoiceEntity { Id = invoiceId, Status = InvoiceStatus.Pending });
        await context.SaveChangesAsync();

        var clientMock = BuildClientMock(success: false, errorReason: "Invoice is already paid.");
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict); // Strict: sẽ fail nếu bị gọi
        var service = BuildService(context, cache, clientMock, publishMock);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = invoiceId, Amount = 5_000m }, "idem-no-publish");

        result.Success.Should().BeFalse();
        // publishMock không được gọi — MockBehavior.Strict sẽ throw nếu có
    }

    // =========================================================================
    // 7. Invoice service timeout → trả về lỗi thân thiện, không lưu payment
    // =========================================================================

    [Fact]
    public async Task ProcessPaymentAsync_WhenInvoiceServiceTimesOut_ReturnsFalse_DoesNotSavePayment()
    {
        using var context = TestDbContextFactory.CreatePaymentDbContext(Guid.NewGuid().ToString());
        var cache = new MemoryCache(new MemoryCacheOptions());

        var invoiceId = Guid.NewGuid();
        context.Invoices.Add(new PaymentInvoiceEntity { Id = invoiceId, Status = InvoiceStatus.Pending });
        await context.SaveChangesAsync();

        var clientMock = new Mock<IRequestClient<IApplyPaymentToInvoiceRequest>>();
        clientMock
            .Setup(c => c.GetResponse<IApplyPaymentToInvoiceResponse>(
                It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<RequestTimeout>()))
            .ThrowsAsync(new RequestTimeoutException());

        var service = BuildService(context, cache, clientMock);

        var result = await service.ProcessPaymentAsync(
            new PaymentEntity { InvoiceId = invoiceId, Amount = 10_500m }, "idem-timeout");

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Contain("time");
        context.Payments.Should().BeEmpty("payment không được lưu khi timeout");
    }
}
