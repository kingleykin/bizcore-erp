using Microsoft.AspNetCore.Authorization;

namespace Bizcore.BuildingBlocks.Authorization
{
    /// <summary>
    /// Attribute khai báo yêu cầu permission trên controller/action.
    /// Tương đương [Authorize(Policy = "permissionCode")] nhưng rõ ràng hơn về ý nghĩa.
    /// 
    /// Usage:
    ///   [RequirePermission(Permissions.Invoice.View)]
    ///   [RequirePermission("Invoice.Create")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequirePermissionAttribute : AuthorizeAttribute
    {
        public RequirePermissionAttribute(string permission)
            : base(permission)
        {
        }
    }
}
