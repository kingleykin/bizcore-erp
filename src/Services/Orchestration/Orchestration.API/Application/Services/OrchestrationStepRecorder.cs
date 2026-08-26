using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orchestration.API.Domain.Entities;
using Orchestration.API.Infrastructure.Data;

namespace Orchestration.API.Application.Services;

/// <summary>
/// Ghi nhận một bước trong ProcessFlow (theo dõi luồng Invoice → Payment → Outcome).
///
/// CHỈ được gọi trực tiếp từ MassTransit consumer (Consume() đã tự nằm trong 1 transaction sẵn
/// nhờ Transactional Inbox) — KHÔNG được bọc lại bằng MediatR/ITransactionalCommand, vì
/// TransactionBehavior sẽ cố mở thêm 1 transaction nữa trên cùng connection và throw
/// "The connection is already in a transaction". Đây từng là bug thật: 3 consumer
/// (InvoiceCreated/PaymentCompleted/PaymentCompensationRequested)OrchestrationConsumer trước đây
/// gọi qua RecordOrchestrationStepCommand (ITransactionalCommand) nên chưa từng ghi được dòng
/// ProcessFlow/FlowStep nào — lỗi bị nuốt bởi MassTransit's UseMessageRetry rồi rơi vào dead-letter,
/// không ai để ý vì đây chỉ là observability, không chặn luồng nghiệp vụ chính.
/// </summary>
public interface IOrchestrationStepRecorder
{
    Task RecordAsync(
        Guid invoiceId,
        string stepType,
        string newState,
        object payload,
        Guid? paymentId,
        CancellationToken ct);
}

public class OrchestrationStepRecorder : IOrchestrationStepRecorder
{
    private readonly AppDbContext _db;

    public OrchestrationStepRecorder(AppDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(
        Guid invoiceId,
        string stepType,
        string newState,
        object payload,
        Guid? paymentId,
        CancellationToken ct)
    {
        var flow = await _db.ProcessFlows
            .Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.InvoiceId == invoiceId, ct);

        if (flow == null)
        {
            flow = ProcessFlow.Create(invoiceId);
            flow.MoveToState(newState, paymentId);
            _db.ProcessFlows.Add(flow);
        }
        else
        {
            flow.MoveToState(newState, paymentId);
        }

        // Add tường minh để EF track là Added, không phải Modified qua relationship fixup.
        var newStep = flow.AddStep(stepType, JsonSerializer.Serialize(payload));
        _db.Set<FlowStep>().Add(newStep);

        await _db.SaveChangesAsync(ct);
    }
}
