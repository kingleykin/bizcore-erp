using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using Customer.API.Application.DTOs;
using Customer.API.Application.Commands;
using Customer.API.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace Customer.API.Controllers;

/// <summary>
/// Quản lý Khách hàng (Customer).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission(Permissions.Customer.View)]
    [ProducesResponseType(typeof(IEnumerable<CustomerResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? customerGroupId = null)
    {
        var result = await _mediator.Send(new GetCustomersQuery(customerGroupId));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Customer.View)]
    [ProducesResponseType(typeof(CustomerResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetCustomerByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.Customer.Create)]
    [ProducesResponseType(typeof(CustomerResponseDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request)
    {
        var result = await _mediator.Send(new CreateCustomerCommand(request));
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Customer.Update)]
    [ProducesResponseType(typeof(CustomerResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        var result = await _mediator.Send(new UpdateCustomerCommand(id, request));
        return Ok(result);
    }

    [HttpPut("{id:guid}/group")]
    [RequirePermission(Permissions.Customer.Update)]
    [ProducesResponseType(typeof(CustomerResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ChangeGroup(Guid id, [FromBody] ChangeCustomerGroupRequest request)
    {
        var result = await _mediator.Send(new ChangeCustomerGroupCommand(id, request.CustomerGroupId));
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(Permissions.Customer.Update)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var success = await _mediator.Send(new DeactivateCustomerCommand(id));
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(Permissions.Customer.Update)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Activate(Guid id)
    {
        var success = await _mediator.Send(new ActivateCustomerCommand(id));
        return success ? NoContent() : NotFound();
    }
}
