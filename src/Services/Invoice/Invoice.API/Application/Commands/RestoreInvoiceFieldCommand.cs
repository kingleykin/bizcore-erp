using MediatR;
using System.Security.Claims;
using Invoice.API.Application.Services;

namespace Invoice.API.Application.Commands
{
    public record RestoreInvoiceFieldCommand(
        Guid InvoiceId,
        string Field,
        string PreviousValue,
        Guid SourceAuditEntryId,
        string Reason,
        ClaimsPrincipal Actor
    ) : IRequest<RestoreFieldResult>;
}
