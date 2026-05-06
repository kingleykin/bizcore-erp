using Bizcore.BuildingBlocks.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Bizcore.UnitTests;

public class CorrelationIdMiddlewareTests
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    // -------------------------------------------------------------------------
    // Helper: tạo middleware + HttpContext, cho phép capture context ở next
    // -------------------------------------------------------------------------
    private static (CorrelationIdMiddleware middleware, HttpContext httpContext) CreateSut(
        Func<HttpContext, Task>? nextCapture = null)
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCapture?.Invoke(context);
            return Task.CompletedTask;
        });
        return (middleware, context);
    }

    // =========================================================================
    // 1. Sinh ID mới khi request không có header
    // =========================================================================

    [Fact]
    public async Task InvokeAsync_WhenNoHeaderProvided_GeneratesNewCorrelationId()
    {
        var (middleware, context) = CreateSut();

        await middleware.InvokeAsync(context);

        var responseHeader = context.Response.Headers[CorrelationIdHeader].ToString();
        responseHeader.Should().NotBeNullOrEmpty();
        Guid.TryParse(responseHeader, out _).Should().BeTrue("ID tự sinh phải là GUID hợp lệ");
    }

    [Fact]
    public async Task InvokeAsync_WhenNoHeaderProvided_StoresIdInContextItems()
    {
        var (middleware, context) = CreateSut();

        await middleware.InvokeAsync(context);

        context.Items[CorrelationIdHeader].Should().NotBeNull();
        context.Items[CorrelationIdHeader]!.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoHeaderProvided_ResponseHeaderMatchesContextItem()
    {
        var (middleware, context) = CreateSut();

        await middleware.InvokeAsync(context);

        var responseHeader = context.Response.Headers[CorrelationIdHeader].ToString();
        var contextItem = context.Items[CorrelationIdHeader]!.ToString();

        responseHeader.Should().Be(contextItem, "response header và context item phải là cùng một ID");
    }

    // =========================================================================
    // 2. Giữ nguyên ID khi request đã có header
    // =========================================================================

    [Fact]
    public async Task InvokeAsync_WhenHeaderProvided_PreservesExistingCorrelationId()
    {
        var existingId = "trace-abc-123";
        var (middleware, context) = CreateSut();
        context.Request.Headers[CorrelationIdHeader] = existingId;

        await middleware.InvokeAsync(context);

        context.Response.Headers[CorrelationIdHeader].ToString().Should().Be(existingId);
        context.Items[CorrelationIdHeader]!.ToString().Should().Be(existingId);
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderIsGuid_PreservesGuidCorrelationId()
    {
        var existingId = Guid.NewGuid().ToString();
        var (middleware, context) = CreateSut();
        context.Request.Headers[CorrelationIdHeader] = existingId;

        await middleware.InvokeAsync(context);

        context.Response.Headers[CorrelationIdHeader].ToString().Should().Be(existingId);
    }

    [Theory]
    [InlineData("  ")]
    [InlineData("")]
    public async Task InvokeAsync_WhenHeaderIsEmptyOrWhitespace_GeneratesNewId(string emptyValue)
    {
        var (middleware, context) = CreateSut();
        context.Request.Headers[CorrelationIdHeader] = emptyValue;

        await middleware.InvokeAsync(context);

        var responseHeader = context.Response.Headers[CorrelationIdHeader].ToString();
        responseHeader.Should().NotBeNullOrEmpty("phải sinh ID mới khi header rỗng");
    }

    // =========================================================================
    // 3. ID được truyền xuống pipeline (next middleware nhìn thấy)
    // =========================================================================

    [Fact]
    public async Task InvokeAsync_CorrelationIdIsAvailableInsideNextMiddleware()
    {
        string? capturedId = null;
        var (middleware, context) = CreateSut(ctx =>
        {
            capturedId = ctx.Items[CorrelationIdHeader]?.ToString();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        capturedId.Should().NotBeNullOrEmpty("next middleware phải thấy được CorrelationId trong Items");
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderProvided_NextMiddlewareReceivesSameId()
    {
        var existingId = "my-trace-id-999";
        string? capturedId = null;
        var (middleware, context) = CreateSut(ctx =>
        {
            capturedId = ctx.Items[CorrelationIdHeader]?.ToString();
            return Task.CompletedTask;
        });
        context.Request.Headers[CorrelationIdHeader] = existingId;

        await middleware.InvokeAsync(context);

        capturedId.Should().Be(existingId);
    }

    // =========================================================================
    // 4. Mỗi request nhận ID độc lập (không bị leak giữa các request)
    // =========================================================================

    [Fact]
    public async Task InvokeAsync_TwoRequestsWithoutHeader_EachGetUniqueId()
    {
        var context1 = new DefaultHttpContext();
        var context2 = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context1);
        await middleware.InvokeAsync(context2);

        var id1 = context1.Response.Headers[CorrelationIdHeader].ToString();
        var id2 = context2.Response.Headers[CorrelationIdHeader].ToString();

        id1.Should().NotBe(id2, "mỗi request phải có ID riêng biệt");
    }

    // =========================================================================
    // 5. Pipeline tiếp tục bình thường
    // =========================================================================

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNextDelegate()
    {
        var nextCallCount = 0;
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCallCount.Should().Be(1, "next delegate phải được gọi đúng 1 lần");
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ExceptionPropagates()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => throw new InvalidOperationException("downstream error"));

        var act = async () => await middleware.InvokeAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("downstream error");
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ResponseHeaderStillSet()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => throw new InvalidOperationException("downstream error"));

        try { await middleware.InvokeAsync(context); } catch { /* expected */ }

        // Header phải được set TRƯỚC khi gọi next
        context.Response.Headers[CorrelationIdHeader].ToString().Should().NotBeNullOrEmpty();
    }
}

// =============================================================================
// CorrelationIdPropagationMiddleware Tests (dùng cho downstream services)
// =============================================================================

public class CorrelationIdPropagationMiddlewareTests
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    // =========================================================================
    // 1. Đọc ID từ request header (do Gateway inject)
    // =========================================================================

    [Fact]
    public async Task InvokeAsync_WhenHeaderProvided_PreservesExistingCorrelationId()
    {
        var existingId = "gateway-injected-id";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdHeader] = existingId;
        var middleware = new CorrelationIdPropagationMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Items[CorrelationIdHeader]!.ToString().Should().Be(existingId);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoHeaderProvided_GeneratesNewId()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdPropagationMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var itemId = context.Items[CorrelationIdHeader]!.ToString();
        itemId.Should().NotBeNullOrEmpty();
        Guid.TryParse(itemId, out _).Should().BeTrue();
    }

    // =========================================================================
    // 2. KHÔNG set Response.Headers (khác với CorrelationIdMiddleware)
    // =========================================================================

    [Fact]
    public async Task InvokeAsync_DoesNotSetResponseHeader()
    {
        var existingId = "gateway-id";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdHeader] = existingId;
        var middleware = new CorrelationIdPropagationMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        // Propagation middleware KHÔNG set response header
        context.Response.Headers.ContainsKey(CorrelationIdHeader).Should().BeFalse(
            "downstream service không cần trả header về, Gateway sẽ lo");
    }

    // =========================================================================
    // 3. ID được truyền xuống pipeline
    // =========================================================================

    [Fact]
    public async Task InvokeAsync_CorrelationIdIsAvailableInsideNextMiddleware()
    {
        var existingId = "trace-123";
        string? capturedId = null;
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdHeader] = existingId;
        var middleware = new CorrelationIdPropagationMiddleware(ctx =>
        {
            capturedId = ctx.Items[CorrelationIdHeader]?.ToString();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        capturedId.Should().Be(existingId);
    }

    // =========================================================================
    // 4. Pipeline tiếp tục bình thường
    // =========================================================================

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNextDelegate()
    {
        var nextCallCount = 0;
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdPropagationMiddleware(_ =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCallCount.Should().Be(1);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ExceptionPropagates()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdPropagationMiddleware(_ => throw new InvalidOperationException("downstream error"));

        var act = async () => await middleware.InvokeAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("downstream error");
    }
}
