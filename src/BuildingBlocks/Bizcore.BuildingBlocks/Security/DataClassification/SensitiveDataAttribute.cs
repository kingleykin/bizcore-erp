using System;

namespace Bizcore.BuildingBlocks.Security.DataClassification
{
    public enum ClassificationLevel
    {
        Public,      // Ví dụ: InvoiceNo -> Log Full
        Internal,    // Ví dụ: CustomerName -> Log Partial (chưa implement partial, hiện tại vẫn log full nếu không mask)
        Sensitive,   // Ví dụ: Email -> Masked
        Restricted   // Ví dụ: Password -> Never Log
    }

    /// <summary>
    /// Đánh dấu một thuộc tính chứa dữ liệu nhạy cảm cần được mask (PII, Passwords, etc.) khi ghi log hoặc trace.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public class SensitiveDataAttribute : Attribute
    {
        public string Mask { get; }
        public ClassificationLevel Level { get; }

        public SensitiveDataAttribute(ClassificationLevel level = ClassificationLevel.Sensitive, string mask = "***")
        {
            Level = level;
            Mask = mask;
        }
    }
}
