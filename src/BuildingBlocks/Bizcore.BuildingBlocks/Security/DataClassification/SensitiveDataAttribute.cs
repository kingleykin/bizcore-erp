using System;

namespace Bizcore.BuildingBlocks.Security.DataClassification
{
    /// <summary>
    /// Đánh dấu một thuộc tính chứa dữ liệu nhạy cảm cần được mask (PII, Passwords, etc.) khi ghi log hoặc trace.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public class SensitiveDataAttribute : Attribute
    {
        public string Mask { get; }

        public SensitiveDataAttribute(string mask = "***")
        {
            Mask = mask;
        }
    }
}
