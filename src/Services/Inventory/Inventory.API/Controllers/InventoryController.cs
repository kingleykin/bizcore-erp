using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using Inventory.API.Application.Commands;
using Inventory.API.Application.DTOs;
using Inventory.API.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.API.Controllers;

/// <summary>
/// Quản lý Tồn kho (Inventory) theo sản phẩm.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission(Permissions.Inventory.View)]
    [ProducesResponseType(typeof(IEnumerable<StockResponseDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetStocksQuery());
        return Ok(result);
    }

    [HttpGet("{productId:guid}")]
    [RequirePermission(Permissions.Inventory.View)]
    [ProducesResponseType(typeof(StockResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByProductId(Guid productId)
    {
        var result = await _mediator.Send(new GetStockByProductIdQuery(productId));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{productId:guid}")]
    [RequirePermission(Permissions.Inventory.Update)]
    [ProducesResponseType(typeof(StockResponseDto), 200)]
    public async Task<IActionResult> AdjustStock(Guid productId, [FromBody] AdjustStockRequest request)
    {
        var result = await _mediator.Send(new AdjustStockCommand(productId, request));
        return Ok(result);
    }

    [HttpGet("transactions")]
    [RequirePermission(Permissions.Inventory.View)]
    [ProducesResponseType(typeof(IEnumerable<StockTransactionDto>), 200)]
    public async Task<IActionResult> GetTransactions([FromQuery] Guid? productId)
    {
        var result = await _mediator.Send(new GetStockTransactionsQuery(productId));
        return Ok(result);
    }
}
