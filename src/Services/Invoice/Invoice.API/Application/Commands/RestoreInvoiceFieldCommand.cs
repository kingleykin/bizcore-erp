using MediatR;
using System.Security.Claims;
using Bizcore.BuildingBlocks.Abstractions;

namespace Invoice.API.Application.Commands;

public record RestoreFieldResult(bool Success, string Message, Guid? NewAuditEntryId = null);

public record RestoreInvoiceFieldCommand(
    Guid InvoiceId,
    string Field,
    string PreviousValue,
    Guid SourceAuditEntryId,
    string Reason,
    ClaimsPrincipal Actor
) : IRequest<RestoreFieldResult>, ITransactionalCommand;
