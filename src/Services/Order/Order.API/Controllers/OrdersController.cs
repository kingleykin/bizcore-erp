using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.API.Application.Commands;
using Order.API.Application.DTOs;
using Order.API.Application.Queries;

namespace Order.API.Controllers;

/// <summary>
/// Quản lý Đơn hàng (Order) của khách hàng.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission(Permissions.Order.View)]
    [ProducesResponseType(typeof(IEnumerable<OrderResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? customerId = null)
    {
        var result = await _mediator.Send(new GetOrdersQuery(customerId));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Order.View)]
    [ProducesResponseType(typeof(OrderResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetOrderByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.Order.Create)]
    [ProducesResponseType(typeof(OrderResponseDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        var result = await _mediator.Send(new CreateOrderCommand(request));
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    [HttpPost("{id:guid}/confirm")]
    [RequirePermission(Permissions.Order.Update)]
    [ProducesResponseType(typeof(OrderResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var result = await _mediator.Send(new ConfirmOrderCommand(id));
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(Permissions.Order.Cancel)]
    [ProducesResponseType(typeof(OrderResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderRequest request)
    {
        var result = await _mediator.Send(new CancelOrderCommand(id, request.Reason));
        return Ok(result);
    }
}
