using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Audit;
using Inventory.API.Application.DTOs;
using Inventory.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Application.Commands;

// Nhập/điều chỉnh tồn kho thủ công (nhập hàng, kiểm kê). Upsert theo ProductId —
// tạo mới Stock nếu sản phẩm đó chưa có bản ghi tồn kho.
public record AdjustStockCommand(Guid ProductId, AdjustStockRequest Request) : IRequest<StockResponseDto>, ITransactionalCommand;

public class AdjustStockHandler : IRequestHandler<AdjustStockCommand, StockResponseDto>
{
    private readonly AppDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<AdjustStockHandler> _logger;

    public AdjustStockHandler(AppDbContext db, IAuditPublisher audit, ILogger<AdjustStockHandler> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<StockResponseDto> Handle(AdjustStockCommand command, CancellationToken ct)
    {
        var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ProductId == command.ProductId, ct);
        var previousOnHand = stock?.QuantityOnHand ?? 0;

        if (stock == null)
        {
            stock = Domain.Entities.Stock.Create(command.ProductId, command.Request.ProductName, command.Request.QuantityOnHand);
            _db.Stocks.Add(stock);
        }
        else
        {
            stock.AdjustOnHand(command.Request.QuantityOnHand);
        }

        _db.StockTransactions.Add(Domain.Entities.StockTransaction.CreateFor(
            stock, Domain.Entities.StockTransactionType.Adjust, quantity: stock.QuantityOnHand - previousOnHand));

        await _audit.PublishAsync(
            AuditActions.Inventory.Adjusted,
            entityType: "Stock",
            entityId: stock.Id.ToString(),
            after: new { stock.Id, stock.ProductId, stock.QuantityOnHand },
            category: AuditCategory.Business,
            classification: DataClassification.Internal,
            ct: ct);

        _logger.LogInformation("StockAdjusted ProductId={ProductId}, OnHand={OnHand}", stock.ProductId, stock.QuantityOnHand);

        return stock.ToDto();
    }
}
