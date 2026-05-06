using Bizcore.BuildingBlocks.Contracts;
using Orchestration.API.Domain.Entities;

namespace Orchestration.API.Application.Services;

public interface IProcessOrchestrationService
{
    Task<ProcessFlow?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessFlow>> ListRecentAsync(int take, CancellationToken cancellationToken = default);
    Task RecordInvoiceCreatedAsync(IInvoiceCreatedEvent e, CancellationToken cancellationToken = default);
    Task RecordPaymentCompletedAsync(IPaymentCompletedEvent e, CancellationToken cancellationToken = default);
    Task RecordCompensationRequestedAsync(IPaymentCompensationRequestedEvent e, CancellationToken cancellationToken = default);
}
