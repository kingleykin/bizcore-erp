namespace Audit.API.Domain.Enums
{
    /// <summary>
    /// Classifies the functional category of an audit entry.
    /// </summary>
    public enum AuditCategory
    {
        Security = 1,
        Business = 2,
        Financial = 3,
        Compliance = 4,
        System = 5
    }

    public enum AuditSeverity
    {
        Info = 1,
        Warning = 2,
        Critical = 3
    }

    public enum AuditOutcome
    {
        Success = 1,
        Failure = 2,
        Denied = 3
    }

    public enum DataClassification
    {
        Public = 1,
        Internal = 2,
        PII = 3,
        Financial = 4,
        Credential = 5
    }
}
