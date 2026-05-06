namespace Orchestration.API.DTOs;

public record FlowStepDto(Guid Id, string StepType, string PayloadJson, DateTime OccurredAtUtc);

public record ProcessFlowDto(
    Guid Id,
    Guid InvoiceId,
    string FlowType,
    string CurrentState,
    Guid? LastPaymentId,
    DateTime StartedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<FlowStepDto> Steps);
