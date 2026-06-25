using Microsoft.AspNetCore.Authorization;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using Customer.API.Application.Commands.CustomerGroup;
using Customer.API.Application.Queries.CustomerGroup;
using Customer.API.Application.DTOs;
using MediatR;

namespace Customer.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/customergroup")]
[ApiVersion("1.0")]
[Authorize]
public class CustomerGroupsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomerGroupsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission(Permissions.Customer.View)]
    public async Task<ActionResult<IEnumerable<CustomerGroupResponseDto>>> GetCustomerGroups()
    {
        var result = await _mediator.Send(new GetCustomerGroupsQuery());
        return Ok(result);
    }

    [HttpGet("{id}")]
    [RequirePermission(Permissions.Customer.View)]
    public async Task<ActionResult<CustomerGroupResponseDto>> GetCustomerGroup(Guid id)
    {
        var result = await _mediator.Send(new GetCustomerGroupByIdQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.Customer.Create)]
    public async Task<ActionResult<CustomerGroupResponseDto>> CreateCustomerGroup([FromBody] CreateCustomerGroupCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetCustomerGroup), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [RequirePermission(Permissions.Customer.Update)]
    public async Task<ActionResult<CustomerGroupResponseDto>> UpdateCustomerGroup(Guid id, [FromBody] UpdateCustomerGroupCommand command)
    {
        if (id != command.Id) return BadRequest("ID mismatch");
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [RequirePermission(Permissions.Customer.Delete)]
    public async Task<ActionResult> DeleteCustomerGroup(Guid id)
    {
        var result = await _mediator.Send(new DeleteCustomerGroupCommand(id));
        if (!result) return NotFound();
        return NoContent();
    }
}
