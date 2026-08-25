namespace Bizcore.BuildingBlocks.Messaging;

/// <summary>
/// Centralized Service-Level Queue Names.
/// In an enterprise architecture, queues represent service boundaries (Consumer Groups),
/// not individual business actions.
/// </summary>
public static class QueueNames
{
    public const string PaymentService = "payment-service";
    public const string InvoiceService = "invoice-service";
    public const string OrchestrationService = "orchestration-service";
    public const string AdminService = "admin-service";
    public const string AuditService = "audit-service";
    public const string ReportService = "report-service";
    public const string CustomerService = "customer-service";
    public const string OrderService = "order-service";
    public const string ProductService = "product-service";
}
