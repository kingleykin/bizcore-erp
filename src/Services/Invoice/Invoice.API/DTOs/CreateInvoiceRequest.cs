namespace Invoice.API.DTOs
{
    public record CreateInvoiceRequest(
        string CustomerName,
        decimal Amount
    );
}
