using Orchestration.API.Domain;

namespace Orchestration.API.Domain.Entities;

/// <summary>
/// One business process instance keyed by invoice (invoice → payment → outcome).
/// </summary>
public class ProcessFlow
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public string FlowType { get; set; } = InvoicePaymentFlow.FlowTypeConstant;
    public string CurrentState { get; set; } = InvoicePaymentFlow.States.InvoiceIndexed;
    public DateTime StartedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? LastPaymentId { get; set; }

    public ICollection<FlowStep> Steps { get; set; } = new List<FlowStep>();
}
