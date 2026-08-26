using System.Reflection;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bizcore.UnitTests;

/// <summary>
/// Test "phòng thủ" cấp RBAC: khẳng định mỗi action ghi/đọc dữ liệu Order/Product/Inventory đều
/// được gắn [RequirePermission] đúng permission mong đợi, và mọi permission đó thực sự tồn tại
/// trong danh mục seed của Admin.API — chính là lớp bug đã từng xảy ra trong dự án này (thêm
/// permission mới ở Permissions.cs nhưng quên seed ở Admin.API/DbSeeder.cs khiến 403 Forbidden dù
/// đã đăng nhập đúng vai trò). Không cần chạy service thật hay Testcontainers, chỉ dùng reflection.
/// </summary>
public class ControllerPermissionTests
{
    private static readonly Type[] Controllers =
    [
        typeof(Order.API.Controllers.OrdersController),
        typeof(Product.API.Controllers.ProductsController),
        typeof(Inventory.API.Controllers.InventoryController)
    ];

    public static IEnumerable<object[]> OrderActionPermissions()
    {
        yield return [nameof(Order.API.Controllers.OrdersController.GetAll), Permissions.Order.View];
        yield return [nameof(Order.API.Controllers.OrdersController.GetById), Permissions.Order.View];
        yield return [nameof(Order.API.Controllers.OrdersController.Create), Permissions.Order.Create];
        yield return [nameof(Order.API.Controllers.OrdersController.Confirm), Permissions.Order.Update];
        yield return [nameof(Order.API.Controllers.OrdersController.Cancel), Permissions.Order.Cancel];
    }

    public static IEnumerable<object[]> ProductActionPermissions()
    {
        yield return [nameof(Product.API.Controllers.ProductsController.GetAll), Permissions.Product.View];
        yield return [nameof(Product.API.Controllers.ProductsController.GetById), Permissions.Product.View];
        yield return [nameof(Product.API.Controllers.ProductsController.Create), Permissions.Product.Create];
        yield return [nameof(Product.API.Controllers.ProductsController.Update), Permissions.Product.Update];
        yield return [nameof(Product.API.Controllers.ProductsController.Deactivate), Permissions.Product.Update];
        yield return [nameof(Product.API.Controllers.ProductsController.Activate), Permissions.Product.Update];
    }

    public static IEnumerable<object[]> InventoryActionPermissions()
    {
        yield return [nameof(Inventory.API.Controllers.InventoryController.GetAll), Permissions.Inventory.View];
        yield return [nameof(Inventory.API.Controllers.InventoryController.GetByProductId), Permissions.Inventory.View];
        yield return [nameof(Inventory.API.Controllers.InventoryController.AdjustStock), Permissions.Inventory.Update];
        yield return [nameof(Inventory.API.Controllers.InventoryController.GetTransactions), Permissions.Inventory.View];
    }

    private static string GetRequiredPermission(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == methodName);
        var attribute = method.GetCustomAttribute<RequirePermissionAttribute>();
        attribute.Should().NotBeNull($"{controllerType.Name}.{methodName} phải có [RequirePermission]");
        return attribute!.Policy!;
    }

    [Theory]
    [MemberData(nameof(OrderActionPermissions))]
    public void OrdersController_Action_RequiresExpectedPermission(string methodName, string expectedPermission)
    {
        GetRequiredPermission(typeof(Order.API.Controllers.OrdersController), methodName)
            .Should().Be(expectedPermission);
    }

    [Theory]
    [MemberData(nameof(ProductActionPermissions))]
    public void ProductsController_Action_RequiresExpectedPermission(string methodName, string expectedPermission)
    {
        GetRequiredPermission(typeof(Product.API.Controllers.ProductsController), methodName)
            .Should().Be(expectedPermission);
    }

    [Theory]
    [MemberData(nameof(InventoryActionPermissions))]
    public void InventoryController_Action_RequiresExpectedPermission(string methodName, string expectedPermission)
    {
        GetRequiredPermission(typeof(Inventory.API.Controllers.InventoryController), methodName)
            .Should().Be(expectedPermission);
    }

    [Theory]
    [MemberData(nameof(ControllerTypes))]
    public void Controller_HasClassLevelAuthorize(Type controllerType)
    {
        controllerType.GetCustomAttribute<AuthorizeAttribute>()
            .Should().NotBeNull($"{controllerType.Name} phải có [Authorize] ở cấp class — không có endpoint public ẩn ngoài ý muốn");
    }

    public static IEnumerable<object[]> ControllerTypes() => Controllers.Select(t => new object[] { t });

    /// <summary>
    /// Đối chiếu MỌI permission mà 3 controller trên yêu cầu với danh mục seed thực tế của
    /// Admin.API (DbSeeder.AllPermissions) — bug gốc từng gặp: Permissions.cs có hằng số mới
    /// nhưng DbSeeder.cs quên thêm PermDef tương ứng, nên role Admin không có quyền đó trong DB,
    /// gây 403 dù code đã đúng. Đọc field private qua reflection vì AllPermissions không public —
    /// chấp nhận đánh đổi (test hơi "biết chi tiết cài đặt") để đổi lấy khả năng bắt đúng lớp bug này.
    /// </summary>
    [Fact]
    public void AllRequiredPermissions_AreSeededInAdminDbSeeder()
    {
        var requiredPermissions = OrderActionPermissions()
            .Concat(ProductActionPermissions())
            .Concat(InventoryActionPermissions())
            .Select(row => (string)row[1])
            .Distinct()
            .ToList();

        var seederType = typeof(Admin.API.Infrastructure.Data.DbSeeder);
        var field = seederType.GetField("AllPermissions", BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull("DbSeeder phải có danh sách AllPermissions — nếu tên field đổi, cập nhật test này theo");

        var rawArray = (System.Collections.IEnumerable)field!.GetValue(null)!;
        var seededCodes = new List<string>();
        foreach (var permDef in rawArray)
        {
            var codeProperty = permDef!.GetType().GetProperty("Code")!;
            seededCodes.Add((string)codeProperty.GetValue(permDef)!);
        }

        foreach (var permission in requiredPermissions)
        {
            seededCodes.Should().Contain(permission,
                $"permission '{permission}' được controller yêu cầu nhưng chưa được seed trong Admin.API/DbSeeder.cs — " +
                "sẽ gây 403 Forbidden dù user có vai trò Admin, kể cả khi code hoàn toàn đúng.");
        }
    }
}
