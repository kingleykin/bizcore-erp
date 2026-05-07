namespace Bizcore.BuildingBlocks
{
    public static class Permissions
    {
        public static class Invoice
        {
            public const string View = "invoice:view";
            public const string Create = "invoice:create";
            public const string Update = "invoice:update";
            public const string Delete = "invoice:delete";
        }

        public static class Payment
        {
            public const string View = "payment:view";
            public const string Create = "payment:create";
            public const string Process = "payment:process";
        }

        public static class Report
        {
            public const string View = "report:view";
            public const string Export = "report:export";
        }

        public static class Orchestration
        {
            public const string View = "orchestration:view";
        }

        public static class Identity
        {
            public static class Users
            {
                public const string View = "identity:users:view";
                public const string Create = "identity:users:create";
                public const string Update = "identity:users:update";
                public const string Delete = "identity:users:delete";
                public const string ManageRoles = "identity:users:manage_roles";
            }

            public static class Roles
            {
                public const string View = "identity:roles:view";
                public const string Create = "identity:roles:create";
                public const string Update = "identity:roles:update";
                public const string Delete = "identity:roles:delete";
                public const string ManagePermissions = "identity:roles:manage_permissions";
            }
        }

        public static class Audit
        {
            public const string View        = "audit:view";
            public const string Export      = "audit:export";
            /// <summary>Cho phép reverse kể cả entity đang ở trạng thái đóng/đã thanh toán.</summary>
            public const string SuperReverse = "audit:super-reverse";
        }
    }
}
