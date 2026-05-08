using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Bizcore.BuildingBlocks.Authorization
{
    /// <summary>
    /// Dynamic Authorization Policy Provider.
    /// Tự động tạo policy runtime cho bất kỳ permission name nào.
    /// 
    /// Thay vì phải khai báo:
    ///   options.AddPolicy("Invoice.View", p => p.RequireClaim("permission", "Invoice.View"));
    /// 
    /// Chỉ cần dùng:
    ///   [Authorize(Policy = "Invoice.View")]
    /// 
    /// Provider tự động tạo policy tương ứng với PermissionRequirement("Invoice.View").
    /// </summary>
    public class DynamicAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        private readonly AuthorizationOptions _options;

        public DynamicAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
            : base(options)
        {
            _options = options.Value;
        }

        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            // Kiểm tra policy đã được khai báo tĩnh (fallback)
            var policy = await base.GetPolicyAsync(policyName);
            if (policy != null)
                return policy;

            // Dynamic: tạo policy từ tên policy = permission code
            // Convention: policy name phải chứa dấu chấm hoặc khớp với permission pattern
            if (!string.IsNullOrWhiteSpace(policyName))
            {
                var dynamicPolicy = new AuthorizationPolicyBuilder()
                    .AddRequirements(new PermissionRequirement(policyName))
                    .Build();

                // Cache lại để không build lại mỗi request
                _options.AddPolicy(policyName, dynamicPolicy);

                return dynamicPolicy;
            }

            return null;
        }
    }
}
