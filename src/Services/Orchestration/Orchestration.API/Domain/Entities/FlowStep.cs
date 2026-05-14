using Bizcore.BuildingBlocks.Abstractions;

namespace Orchestration.API.Domain.Entities;

/// <summary>
/// Immutable timeline entry appended when a domain event is observed by the orchestrator.
/// </summary>
public class FlowStep : BaseEntity
{
    public Guid ProcessFlowId { get; set; }
    public ProcessFlow ProcessFlow { get; set; } = null!;
    public string StepType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}
