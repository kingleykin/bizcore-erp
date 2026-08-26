using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Product.API.Application.Commands;
using Product.API.Application.DTOs;

namespace Bizcore.UnitTests;

public class ProductEventPublishingTests
{
    [Fact]
    public async Task CreateProductHandler_Handle_PersistsProduct_AndPublishesProductCreatedEvent()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateProductDbContext(connection);

        var publishMock = new Mock<IPublishEndpoint>();
        IProductCreatedEvent? published = null;
        publishMock
            .Setup(p => p.Publish<IProductCreatedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = Mock.Of<IProductCreatedEvent>(m =>
                m.Id == (Guid)values.GetType().GetProperty("Id")!.GetValue(values)! &&
                m.Name == (string)values.GetType().GetProperty("Name")!.GetValue(values)!))
            .Returns(Task.CompletedTask);

        var handler = new CreateProductHandler(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<CreateProductHandler>.Instance);

        var result = await handler.Handle(
            new CreateProductCommand(new CreateProductRequest("Sản phẩm mới", "Cái", 99m, "mô tả")),
            CancellationToken.None);

        // Handler không tự SaveChanges (TransactionBehavior/IUnitOfWork.CommitAsync làm việc đó
        // trong pipeline thật) — mô phỏng lại bước đó để assert được trạng thái đã lưu DB.
        await context.SaveChangesAsync();

        context.Products.Should().ContainSingle();
        result.Name.Should().Be("Sản phẩm mới");

        published.Should().NotBeNull("Inventory Service cần event này để tạo sẵn bản ghi tồn kho cho sản phẩm mới");
        published!.Id.Should().Be(result.Id);
        published.Name.Should().Be("Sản phẩm mới");
    }
}
