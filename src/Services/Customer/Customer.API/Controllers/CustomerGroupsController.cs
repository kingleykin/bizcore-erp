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
/// Quản lý Nhóm khách hàng (CustomerGroup).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customer-groups")]
[Authorize]
public class CustomerGroupsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomerGroupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission(Permissions.CustomerGroup.View)]
    [ProducesResponseType(typeof(IEnumerable<CustomerGroupResponseDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetCustomerGroupsQuery());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.CustomerGroup.View)]
    [ProducesResponseType(typeof(CustomerGroupResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetCustomerGroupByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.CustomerGroup.Create)]
    [ProducesResponseType(typeof(CustomerGroupResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerGroupRequest request)
    {
        var result = await _mediator.Send(new CreateCustomerGroupCommand(request));
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.CustomerGroup.Update)]
    [ProducesResponseType(typeof(CustomerGroupResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerGroupRequest request)
    {
        var result = await _mediator.Send(new UpdateCustomerGroupCommand(id, request));
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(Permissions.CustomerGroup.Update)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var success = await _mediator.Send(new DeactivateCustomerGroupCommand(id));
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(Permissions.CustomerGroup.Update)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Activate(Guid id)
    {
        var success = await _mediator.Send(new ActivateCustomerGroupCommand(id));
        return success ? NoContent() : NotFound();
    }
}
