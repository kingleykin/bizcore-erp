using System;

namespace Bizcore.BuildingBlocks.MultiTenancy
{
    public interface ITenantContext
    {
        string? TenantId { get; }
        bool IsMultiTenant => !string.IsNullOrEmpty(TenantId);
    }

    public class TenantContext : ITenantContext
    {
        public string? TenantId { get; internal set; }
    }
}
