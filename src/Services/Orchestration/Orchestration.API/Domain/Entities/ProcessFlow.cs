using Bizcore.BuildingBlocks.Abstractions;
using Orchestration.API.Domain;

namespace Orchestration.API.Domain.Entities;

/// <summary>
/// One business process instance keyed by invoice (invoice → payment → outcome).
/// </summary>
public class ProcessFlow : AggregateRoot
{
    public Guid InvoiceId { get; private set; }

    public string FlowType { get; private set; } = InvoicePaymentFlow.FlowTypeConstant;
    public string CurrentState { get; private set; } = InvoicePaymentFlow.States.InvoiceIndexed;
    public Guid? LastPaymentId { get; private set; }

    private readonly List<FlowStep> _steps = new();
    public virtual IReadOnlyCollection<FlowStep> Steps => _steps.AsReadOnly();

    public static ProcessFlow Create(Guid invoiceId)
    {
        return new ProcessFlow { InvoiceId = invoiceId };
    }

    public void MoveToState(string newState, Guid? paymentId = null)
    {
        CurrentState = newState;
        if (paymentId.HasValue) LastPaymentId = paymentId;
        
        MarkStateChanged();
    }

    public FlowStep AddStep(string stepType, string payload)
    {
        var step = new FlowStep { ProcessFlowId = Id, StepType = stepType, PayloadJson = payload };
        _steps.Add(step);
        MarkStateChanged();
        return step;
    }
}
