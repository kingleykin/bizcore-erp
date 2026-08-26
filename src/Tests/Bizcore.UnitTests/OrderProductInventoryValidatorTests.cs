using FluentAssertions;
using Inventory.API.Application.DTOs;
using Inventory.API.Application.Validators;
using Order.API.Application.DTOs;
using Order.API.Application.Validators;
using Product.API.Application.DTOs;
using Product.API.Application.Validators;

namespace Bizcore.UnitTests;

public class OrderProductInventoryValidatorTests
{
    // ---------- CreateOrderItemRequestValidator ----------

    [Fact]
    public void CreateOrderItemRequestValidator_WithValidData_IsValid()
    {
        var validator = new CreateOrderItemRequestValidator();
        var result = validator.Validate(new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateOrderItemRequestValidator_WithEmptyProductId_IsInvalid()
    {
        var validator = new CreateOrderItemRequestValidator();
        var result = validator.Validate(new CreateOrderItemRequest(Guid.Empty, 1, 10m));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderItemRequest.ProductId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateOrderItemRequestValidator_WithNonPositiveQuantity_IsInvalid(int quantity)
    {
        var validator = new CreateOrderItemRequestValidator();
        var result = validator.Validate(new CreateOrderItemRequest(Guid.NewGuid(), quantity, 10m));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderItemRequest.Quantity));
    }

    [Fact]
    public void CreateOrderItemRequestValidator_WithNegativeUnitPrice_IsInvalid()
    {
        var validator = new CreateOrderItemRequestValidator();
        var result = validator.Validate(new CreateOrderItemRequest(Guid.NewGuid(), 1, -0.01m));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderItemRequest.UnitPrice));
    }

    [Fact]
    public void CreateOrderItemRequestValidator_WithZeroUnitPrice_IsValid()
    {
        var validator = new CreateOrderItemRequestValidator();
        var result = validator.Validate(new CreateOrderItemRequest(Guid.NewGuid(), 1, 0m));
        result.IsValid.Should().BeTrue("giá 0 là biên hợp lệ (khuyến mãi/tặng kèm)");
    }

    // ---------- CreateOrderRequestValidator ----------

    [Fact]
    public void CreateOrderRequestValidator_WithValidData_IsValid()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(Guid.NewGuid(), "ghi chú",
            [new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateOrderRequestValidator_WithEmptyCustomerId_IsInvalid()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(Guid.Empty, null,
            [new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.CustomerId));
    }

    [Fact]
    public void CreateOrderRequestValidator_WithEmptyItems_IsInvalid()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(Guid.NewGuid(), null, []));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.Items));
    }

    [Fact]
    public void CreateOrderRequestValidator_WithNoteTooLong_IsInvalid()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(Guid.NewGuid(), new string('x', 1001),
            [new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderRequest.Note));
    }

    [Fact]
    public void CreateOrderRequestValidator_WithNoteAtMaxLength_IsValid()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(Guid.NewGuid(), new string('x', 1000),
            [new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]));
        result.IsValid.Should().BeTrue("1000 ký tự là biên hợp lệ (MaximumLength(1000))");
    }

    [Fact]
    public void CreateOrderRequestValidator_WithOneInvalidItemAmongMany_IsInvalid_ViaRuleForEach()
    {
        var validator = new CreateOrderRequestValidator();
        var result = validator.Validate(new CreateOrderRequest(Guid.NewGuid(), null,
        [
            new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m),
            new CreateOrderItemRequest(Guid.NewGuid(), 0, 10m) // dòng thứ 2 sai số lượng
        ]));

        result.IsValid.Should().BeFalse("RuleForEach phải validate từng dòng, kể cả khi các dòng khác hợp lệ");
    }

    // ---------- CancelOrderRequestValidator ----------

    [Fact]
    public void CancelOrderRequestValidator_WithValidReason_IsValid()
    {
        var validator = new CancelOrderRequestValidator();
        var result = validator.Validate(new CancelOrderRequest("Khách đổi ý"));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CancelOrderRequestValidator_WithBlankReason_IsInvalid(string reason)
    {
        var validator = new CancelOrderRequestValidator();
        var result = validator.Validate(new CancelOrderRequest(reason));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CancelOrderRequestValidator_WithReasonTooLong_IsInvalid()
    {
        var validator = new CancelOrderRequestValidator();
        var result = validator.Validate(new CancelOrderRequest(new string('x', 501)));
        result.IsValid.Should().BeFalse();
    }

    // ---------- CreateProductRequestValidator ----------

    [Fact]
    public void CreateProductRequestValidator_WithValidData_IsValid()
    {
        var validator = new CreateProductRequestValidator();
        var result = validator.Validate(new CreateProductRequest("Sản phẩm", "Cái", 10m, null));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateProductRequestValidator_WithBlankName_IsInvalid(string name)
    {
        var validator = new CreateProductRequestValidator();
        var result = validator.Validate(new CreateProductRequest(name, "Cái", 10m, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductRequest.Name));
    }

    [Fact]
    public void CreateProductRequestValidator_WithNameTooLong_IsInvalid()
    {
        var validator = new CreateProductRequestValidator();
        var result = validator.Validate(new CreateProductRequest(new string('x', 256), "Cái", 10m, null));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateProductRequestValidator_WithBlankUnit_IsInvalid()
    {
        var validator = new CreateProductRequestValidator();
        var result = validator.Validate(new CreateProductRequest("Sản phẩm", "", 10m, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductRequest.Unit));
    }

    [Fact]
    public void CreateProductRequestValidator_WithNegativePrice_IsInvalid()
    {
        var validator = new CreateProductRequestValidator();
        var result = validator.Validate(new CreateProductRequest("Sản phẩm", "Cái", -1m, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductRequest.Price));
    }

    [Fact]
    public void CreateProductRequestValidator_WithZeroPrice_IsValid()
    {
        var validator = new CreateProductRequestValidator();
        var result = validator.Validate(new CreateProductRequest("Sản phẩm", "Cái", 0m, null));
        result.IsValid.Should().BeTrue();
    }

    // ---------- UpdateProductRequestValidator ----------

    [Fact]
    public void UpdateProductRequestValidator_WithValidData_IsValid()
    {
        var validator = new UpdateProductRequestValidator();
        var result = validator.Validate(new UpdateProductRequest("Sản phẩm", "Cái", 10m, null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateProductRequestValidator_WithNegativePrice_IsInvalid()
    {
        var validator = new UpdateProductRequestValidator();
        var result = validator.Validate(new UpdateProductRequest("Sản phẩm", "Cái", -5m, null));
        result.IsValid.Should().BeFalse();
    }

    // ---------- AdjustStockRequestValidator ----------

    [Fact]
    public void AdjustStockRequestValidator_WithValidData_IsValid()
    {
        var validator = new AdjustStockRequestValidator();
        var result = validator.Validate(new AdjustStockRequest("Sản phẩm", 10));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AdjustStockRequestValidator_WithBlankProductName_IsInvalid(string name)
    {
        var validator = new AdjustStockRequestValidator();
        var result = validator.Validate(new AdjustStockRequest(name, 10));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AdjustStockRequest.ProductName));
    }

    [Fact]
    public void AdjustStockRequestValidator_WithProductNameTooLong_IsInvalid()
    {
        var validator = new AdjustStockRequestValidator();
        var result = validator.Validate(new AdjustStockRequest(new string('x', 256), 10));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AdjustStockRequestValidator_WithNegativeQuantityOnHand_IsInvalid()
    {
        var validator = new AdjustStockRequestValidator();
        var result = validator.Validate(new AdjustStockRequest("Sản phẩm", -1));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(AdjustStockRequest.QuantityOnHand));
    }

    [Fact]
    public void AdjustStockRequestValidator_WithZeroQuantityOnHand_IsValid()
    {
        var validator = new AdjustStockRequestValidator();
        var result = validator.Validate(new AdjustStockRequest("Sản phẩm", 0));
        result.IsValid.Should().BeTrue("0 là biên hợp lệ (hết hàng)");
    }
}
