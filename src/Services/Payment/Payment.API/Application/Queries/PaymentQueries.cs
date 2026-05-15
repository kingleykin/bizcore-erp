using MediatR;
using Microsoft.EntityFrameworkCore;
using Payment.API.Application.DTOs;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Queries;

// 1. Get All Payments
public record GetPaymentsQuery : IRequest<IEnumerable<PaymentResponseDto>>;

public class GetPaymentsHandler : IRequestHandler<GetPaymentsQuery, IEnumerable<PaymentResponseDto>>
{
    private readonly AppDbContext _db;

    public GetPaymentsHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<PaymentResponseDto>> Handle(GetPaymentsQuery request, CancellationToken ct)
    {
        var payments = await _db.Payments.AsNoTracking().ToListAsync(ct);
        return payments.Select(p => p.ToDto());
    }
}

// 2. Get Payment By Id
public record GetPaymentByIdQuery(Guid Id) : IRequest<PaymentResponseDto?>;

public class GetPaymentByIdHandler : IRequestHandler<GetPaymentByIdQuery, PaymentResponseDto?>
{
    private readonly AppDbContext _db;

    public GetPaymentByIdHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PaymentResponseDto?> Handle(GetPaymentByIdQuery request, CancellationToken ct)
    {
        var p = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        return p?.ToDto();
    }
}
