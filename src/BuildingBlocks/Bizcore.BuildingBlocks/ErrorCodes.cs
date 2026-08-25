namespace Bizcore.BuildingBlocks
{
    public static class ErrorCodes
    {
        public static class Common
        {
            public const string InternalError = "COMMON.INTERNAL_ERROR";
            public const string InvalidRequest = "COMMON.INVALID_REQUEST";
            public const string Unauthorized = "COMMON.UNAUTHORIZED";
            public const string Forbidden = "COMMON.FORBIDDEN";
            public const string NotFound = "COMMON.NOT_FOUND";
            public const string ConcurrencyError = "COMMON.CONCURRENCY_ERROR";
            public const string ServiceUnavailable = "COMMON.SERVICE_UNAVAILABLE";
        }

        public static class User
        {
            public const string NotFound = "USER.NOT_FOUND";
            public const string UsernameTaken = "USER.USERNAME_TAKEN";
            public const string EmailTaken = "USER.EMAIL_TAKEN";
            public const string InvalidCredentials = "USER.INVALID_CREDENTIALS";
            public const string AccountLocked = "USER.ACCOUNT_LOCKED";
            public const string AccountInactive = "USER.ACCOUNT_INACTIVE";
        }

        public static class Invoice
        {
            public const string NotFound = "INVOICE.NOT_FOUND";
            public const string InvalidStatus = "INVOICE.INVALID_STATUS";
            public const string AlreadyPaid = "INVOICE.ALREADY_PAID";
        }

        public static class Payment
        {
            public const string Failed = "PAYMENT.FAILED";
            public const string Timeout = "PAYMENT.TIMEOUT";
            public const string InsufficientFunds = "PAYMENT.INSUFFICIENT_FUNDS";
        }

        public static class Customer
        {
            public const string NotFound = "CUSTOMER.NOT_FOUND";
            public const string CodeAlreadyExists = "CUSTOMER.CODE_ALREADY_EXISTS";
        }

        public static class CustomerGroup
        {
            public const string NotFound = "CUSTOMER_GROUP.NOT_FOUND";
            public const string CodeAlreadyExists = "CUSTOMER_GROUP.CODE_ALREADY_EXISTS";
        }

        public static class Order
        {
            public const string NotFound = "ORDER.NOT_FOUND";
            public const string CustomerNotFound = "ORDER.CUSTOMER_NOT_FOUND";
            public const string ProductNotFound = "ORDER.PRODUCT_NOT_FOUND";
            public const string EmptyItems = "ORDER.EMPTY_ITEMS";
            public const string InvalidStatus = "ORDER.INVALID_STATUS";
        }

        public static class Product
        {
            public const string NotFound = "PRODUCT.NOT_FOUND";
            public const string InactiveProduct = "PRODUCT.INACTIVE";
        }
    }
}
