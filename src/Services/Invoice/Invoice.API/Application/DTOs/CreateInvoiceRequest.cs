namespace Invoice.API.Application.DTOs;

public record CreateInvoiceRequest(
    Guid CustomerId,
    string CustomerName,
    decimal Amount
);
