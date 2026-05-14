using Bizcore.BuildingBlocks.Abstractions;
using Orchestration.API.Domain;

namespace Orchestration.API.Domain.Entities;

/// <summary>
/// One business process instance keyed by invoice (invoice → payment → outcome).
/// </summary>
public class ProcessFlow : BaseEntity
{
    public Guid InvoiceId { get; set; }

    public string FlowType { get; set; } = InvoicePaymentFlow.FlowTypeConstant;
    public string CurrentState { get; set; } = InvoicePaymentFlow.States.InvoiceIndexed;
    public Guid? LastPaymentId { get; set; }


    public ICollection<FlowStep> Steps { get; set; } = new List<FlowStep>();
}
