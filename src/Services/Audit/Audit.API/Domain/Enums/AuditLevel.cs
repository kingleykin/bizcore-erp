namespace Audit.API.Domain.Enums
{
    /// <summary>
    /// Classifies the significance of an audit entry.
    /// Drives retention policy, alerting thresholds, and compliance reports.
    /// </summary>
    public enum AuditLevel
    {
        /// <summary>Login, logout, failed attempts, permission changes.</summary>
        Security = 1,

        /// <summary>Payment initiated, completed, reversed, invoice amount changes.</summary>
        Financial = 2,

        /// <summary>Normal CRUD operations: create invoice, update profile.</summary>
        Operational = 3,

        /// <summary>Role assignments, permission grants, system config changes.</summary>
        Compliance = 4
    }
}
