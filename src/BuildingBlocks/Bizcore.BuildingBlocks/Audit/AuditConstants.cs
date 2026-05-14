namespace Bizcore.BuildingBlocks.Audit
{
    public static class AuditCategory
    {
        public const string Security = "security";
        public const string Business = "business";
        public const string Financial = "financial";
        public const string Compliance = "compliance";
        public const string System = "system";
    }

    public static class AuditSeverity
    {
        public const string Info = "info";
        public const string Warning = "warning";
        public const string Critical = "critical";
    }

    public static class AuditOutcome
    {
        public const string Success = "success";
        public const string Failure = "failure";
        public const string Denied = "denied";
    }

    public static class DataClassification
    {
        public const string Public = "public";
        public const string Internal = "internal";
        public const string PII = "pii";
        public const string Financial = "financial";
        public const string Credential = "credential";
    }

    public static class AuditActions
    {
        public static class Identity
        {
            public const string UserCreated = "identity.user.created";
            public const string UserUpdated = "identity.user.updated";
            public const string UserDeactivated = "identity.user.deactivated";
            public const string UserUnlocked = "identity.user.unlocked";
            public const string UserRolesAssigned = "identity.user.roles.assigned";
            
            public const string RoleCreated = "identity.role.created";
            public const string RoleUpdated = "identity.role.updated";
            public const string RoleDeleted = "identity.role.deleted";
            public const string RolePermissionsAssigned = "identity.role.permissions.assigned";

            public const string AuthLoginSucceeded = "identity.auth.login.succeeded";
            public const string AuthLoginFailed = "identity.auth.login.failed";
            public const string AuthPasswordChanged = "identity.auth.password.changed";
            public const string AuthTokenRefreshed = "identity.auth.token.refreshed";
        }

        public static class Invoice
        {
            public const string Created = "invoice.created";
            public const string Updated = "invoice.updated";
            public const string StatusUpdated = "invoice.status.updated";
            public const string FieldRestored = "invoice.field.restored";
        }

        public static class File
        {
            public const string Viewed = "file.access.viewed";
            public const string Uploaded = "file.upload.succeeded";
            public const string Deleted = "file.delete.succeeded";
        }

        public static class Payment
        {
            public const string Initiated = "payment.initiated";
        }
    }
}
