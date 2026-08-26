using Bizcore.BuildingBlocks.Exceptions;
using FluentAssertions;
using ProductEntity = Product.API.Domain.Entities.Product;

namespace Bizcore.UnitTests;

public class ProductDomainTests
{
    [Fact]
    public void Create_WithValidData_TrimsNameAndUnit_SetsActive()
    {
        var product = ProductEntity.Create("  Bàn phím  ", "  Cái  ", 100m, "  mô tả  ");

        product.Name.Should().Be("Bàn phím");
        product.Unit.Should().Be("Cái");
        product.Description.Should().Be("mô tả");
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_GeneratesCode_WithSpPrefixAndTodayDate()
    {
        var product = ProductEntity.Create("Sản phẩm", "Cái", 10m);

        product.Code.Should().StartWith("SP");
        product.Code.Should().Contain(DateTime.UtcNow.ToString("yyMMdd"));
    }

    [Fact]
    public void Create_WithNullDescription_IsAllowed()
    {
        var product = ProductEntity.Create("Sản phẩm", "Cái", 10m, null);
        product.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name)
    {
        var act = () => ProductEntity.Create(name, "Cái", 10m);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankUnit_Throws(string unit)
    {
        var act = () => ProductEntity.Create("Sản phẩm", unit, 10m);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithNegativePrice_Throws()
    {
        var act = () => ProductEntity.Create("Sản phẩm", "Cái", -0.01m);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithZeroPrice_IsValid()
    {
        var product = ProductEntity.Create("Sản phẩm", "Cái", 0m);
        product.Price.Should().Be(0m);
    }

    [Fact]
    public void Update_WithValidData_UpdatesAllFields()
    {
        var product = ProductEntity.Create("Cũ", "Cái", 10m, "mô tả cũ");

        product.Update("Mới", "Hộp", 20m, "mô tả mới");

        product.Name.Should().Be("Mới");
        product.Unit.Should().Be("Hộp");
        product.Price.Should().Be(20m);
        product.Description.Should().Be("mô tả mới");
    }

    [Fact]
    public void Update_WithBlankName_Throws_LeavesOriginalUnchanged()
    {
        var product = ProductEntity.Create("Cũ", "Cái", 10m);

        var act = () => product.Update("", "Cái", 20m, null);

        act.Should().Throw<DomainException>();
        product.Name.Should().Be("Cũ", "cập nhật thất bại thì không được đổi state nửa chừng");
    }

    [Fact]
    public void Update_WithNegativePrice_Throws()
    {
        var product = ProductEntity.Create("Sản phẩm", "Cái", 10m);
        var act = () => product.Update("Sản phẩm", "Cái", -1m, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var product = ProductEntity.Create("Sản phẩm", "Cái", 10m);
        product.Deactivate();

        product.Activate();

        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var product = ProductEntity.Create("Sản phẩm", "Cái", 10m);

        product.Deactivate();

        product.IsActive.Should().BeFalse();
    }
}
