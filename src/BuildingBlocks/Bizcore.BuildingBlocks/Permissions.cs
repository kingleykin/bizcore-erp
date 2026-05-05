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
    }
}
