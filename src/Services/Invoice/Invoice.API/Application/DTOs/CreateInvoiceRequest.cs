namespace Invoice.API.Application.DTOs;

public record CreateInvoiceRequest(
    string CustomerName,
    decimal Amount
);
