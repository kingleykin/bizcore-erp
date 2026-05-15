namespace Invoice.API.Application.DTOs;

public record RestoreFieldRequest(
    string Field,
    string PreviousValue,
    Guid   AuditEntryId,
    string Reason
);
