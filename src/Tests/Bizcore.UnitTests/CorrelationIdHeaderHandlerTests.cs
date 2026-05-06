using Bizcore.BuildingBlocks.DelegatingHandlers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Moq.Protected;

namespace Bizcore.UnitTests;

public class CorrelationIdHeaderHandlerTests
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    // -------------------------------------------------------------------------
    // Helper: capture outgoing request từ inner handler
    // -------------------------------------------------------------------------
    private static (CorrelationIdHeaderHandler handler, Mock<HttpMessageHandler> innerMock)
        CreateSut(IHttpContextAccessor accessor)
    {
        var innerMock = new Mock<HttpMessageHandler>();
        innerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        var handler = new CorrelationIdHeaderHandler(accessor)
        {
            InnerHandler = innerMock.Object
        };

        return (handler, innerMock);
    }

    private static HttpClient BuildClient(CorrelationIdHeaderHandler handler)
        => new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

    // =========================================================================
    // 1. Inject header khi HttpContext có CorrelationId
    // =========================================================================

    [Fact]
    public async Task SendAsync_WhenContextHasCorrelationId_AddsHeaderToOutgoingRequest()
    {
        var correlationId = "trace-xyz-456";
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdHeader] = correlationId;

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        HttpRequestMessage? capturedRequest = null;
        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        var handler = new CorrelationIdHeaderHandler(accessorMock.Object) { InnerHandler = innerMock.Object };
        var client = BuildClient(handler);

        await client.GetAsync("/test");

        capturedRequest!.Headers.Contains(CorrelationIdHeader).Should().BeTrue();
        capturedRequest.Headers.GetValues(CorrelationIdHeader).First().Should().Be(correlationId);
    }

    // =========================================================================
    // 2. Không thêm header khi HttpContext null (background job, v.v.)
    // =========================================================================

    [Fact]
    public async Task SendAsync_WhenHttpContextIsNull_DoesNotAddHeader()
    {
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        HttpRequestMessage? capturedRequest = null;
        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        var handler = new CorrelationIdHeaderHandler(accessorMock.Object) { InnerHandler = innerMock.Object };
        var client = BuildClient(handler);

        await client.GetAsync("/test");

        capturedRequest!.Headers.Contains(CorrelationIdHeader).Should().BeFalse();
    }

    // =========================================================================
    // 3. Không thêm header khi CorrelationId trong Items là null/rỗng
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SendAsync_WhenCorrelationIdIsNullOrEmpty_DoesNotAddHeader(string? idValue)
    {
        var httpContext = new DefaultHttpContext();
        if (idValue != null)
            httpContext.Items[CorrelationIdHeader] = idValue;

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        HttpRequestMessage? capturedRequest = null;
        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        var handler = new CorrelationIdHeaderHandler(accessorMock.Object) { InnerHandler = innerMock.Object };
        var client = BuildClient(handler);

        await client.GetAsync("/test");

        capturedRequest!.Headers.Contains(CorrelationIdHeader).Should().BeFalse();
    }

    // =========================================================================
    // 4. Không ghi đè header nếu request đã có sẵn (caller tự set)
    // =========================================================================

    [Fact]
    public async Task SendAsync_WhenRequestAlreadyHasHeader_DoesNotOverrideIt()
    {
        var contextId = "context-id";
        var presetId = "preset-id-from-caller";

        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdHeader] = contextId;

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        HttpRequestMessage? capturedRequest = null;
        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        var handler = new CorrelationIdHeaderHandler(accessorMock.Object) { InnerHandler = innerMock.Object };
        var client = BuildClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        request.Headers.Add(CorrelationIdHeader, presetId);
        await client.SendAsync(request);

        // Giá trị phải là preset, không bị ghi đè bởi context
        capturedRequest!.Headers.GetValues(CorrelationIdHeader).Should().ContainSingle()
            .Which.Should().Be(presetId);
    }

    // =========================================================================
    // 5. Không được có duplicate header (regression test cho YARP bug)
    // =========================================================================

    [Fact]
    public async Task SendAsync_WhenContextHasCorrelationId_HeaderAppearsExactlyOnce()
    {
        // Regression: YARP copy headers từ incoming request, sau đó transform lại add thêm
        // → phải đảm bảo chỉ có đúng 1 giá trị, không bị duplicate
        var correlationId = "no-duplicate-id";
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdHeader] = correlationId;

        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns(httpContext);

        HttpRequestMessage? capturedRequest = null;
        var innerMock = new Mock<HttpMessageHandler>();
        innerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        var handler = new CorrelationIdHeaderHandler(accessorMock.Object) { InnerHandler = innerMock.Object };
        var client = BuildClient(handler);

        await client.GetAsync("/test");

        capturedRequest!.Headers.GetValues(CorrelationIdHeader)
            .Should().ContainSingle("header X-Correlation-ID không được xuất hiện 2 lần");
    }

    // =========================================================================
    // 6. Inner handler vẫn được gọi trong mọi trường hợp
    // =========================================================================

    [Fact]
    public async Task SendAsync_AlwaysCallsInnerHandler()
    {
        var accessorMock = new Mock<IHttpContextAccessor>();
        accessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var (handler, innerMock) = CreateSut(accessorMock.Object);
        var client = BuildClient(handler);

        await client.GetAsync("/test");

        innerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
