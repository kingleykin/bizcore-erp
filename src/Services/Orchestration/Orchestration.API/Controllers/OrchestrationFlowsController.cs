using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Orchestration.API.Application.Queries;
using Orchestration.API.Application.DTOs;

namespace Orchestration.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/orchestration/flows")]
[ApiVersion("1.0")]
[Authorize(Policy = "Orchestration.View")]
public class OrchestrationFlowsController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrchestrationFlowsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(IReadOnlyList<ProcessFlowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrchestrationFlowsQuery(take), ct);
        return Ok(result);
    }

    [HttpGet("{invoiceId:guid}")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ProcessFlowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByInvoice(Guid invoiceId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetOrchestrationFlowByInvoiceQuery(invoiceId), ct);
        return result == null ? NotFound() : Ok(result);
    }
}
