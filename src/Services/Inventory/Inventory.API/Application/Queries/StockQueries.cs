using Inventory.API.Application.DTOs;
using Inventory.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.API.Application.Queries;

// 1. Get All Stock
public record GetStocksQuery : IRequest<IEnumerable<StockResponseDto>>;

public class GetStocksHandler : IRequestHandler<GetStocksQuery, IEnumerable<StockResponseDto>>
{
    private readonly AppDbContext _db;

    public GetStocksHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<StockResponseDto>> Handle(GetStocksQuery request, CancellationToken ct)
    {
        var entities = await _db.Stocks.AsNoTracking().OrderBy(s => s.ProductName).ToListAsync(ct);
        return entities.Select(e => e.ToDto());
    }
}

// 2. Get Stock By ProductId
public record GetStockByProductIdQuery(Guid ProductId) : IRequest<StockResponseDto?>;

public class GetStockByProductIdHandler : IRequestHandler<GetStockByProductIdQuery, StockResponseDto?>
{
    private readonly AppDbContext _db;

    public GetStockByProductIdHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<StockResponseDto?> Handle(GetStockByProductIdQuery request, CancellationToken ct)
    {
        var entity = await _db.Stocks.AsNoTracking().FirstOrDefaultAsync(s => s.ProductId == request.ProductId, ct);
        return entity?.ToDto();
    }
}

// 3. Get Stock Transactions (lịch sử xuất nhập tồn) — lọc theo sản phẩm nếu có, mới nhất trước.
public record GetStockTransactionsQuery(Guid? ProductId) : IRequest<IEnumerable<StockTransactionDto>>;

public class GetStockTransactionsHandler : IRequestHandler<GetStockTransactionsQuery, IEnumerable<StockTransactionDto>>
{
    private readonly AppDbContext _db;

    public GetStockTransactionsHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<StockTransactionDto>> Handle(GetStockTransactionsQuery request, CancellationToken ct)
    {
        var query = _db.StockTransactions.AsNoTracking().AsQueryable();
        if (request.ProductId.HasValue)
            query = query.Where(t => t.ProductId == request.ProductId.Value);

        var entities = await query.OrderByDescending(t => t.CreatedAt).Take(200).ToListAsync(ct);
        return entities.Select(e => e.ToDto());
    }
}
