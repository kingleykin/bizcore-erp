using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bizcore.BuildingBlocks;
using Orchestration.API.Application.Services;
using Orchestration.API.Domain.Entities;
using Orchestration.API.DTOs;

namespace Orchestration.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/orchestration/flows")]
[ApiVersion("1.0")]
[Authorize(Policy = "Orchestration.View")]
public class OrchestrationFlowsController : ControllerBase
{
    private readonly IProcessOrchestrationService _orchestration;
    private readonly ILogger<OrchestrationFlowsController> _logger;

    public OrchestrationFlowsController(
        IProcessOrchestrationService orchestration,
        ILogger<OrchestrationFlowsController> logger)
    {
        _orchestration = orchestration;
        _logger = logger;
    }

    [HttpGet]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<IReadOnlyList<ProcessFlowDto>>> List([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing recent orchestration flows, Take={Take}", take);
        var flows = await _orchestration.ListRecentAsync(take, cancellationToken);
        _logger.LogInformation("Retrieved {Count} orchestration flows", flows.Count);
        return Ok(flows.Select(Map).ToList());
    }

    [HttpGet("{invoiceId:guid}")]
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<ActionResult<ProcessFlowDto>> GetByInvoice(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving orchestration flow for InvoiceId={InvoiceId}", invoiceId);
        var flow = await _orchestration.GetByInvoiceIdAsync(invoiceId, cancellationToken);
        if (flow == null)
        {
            _logger.LogWarning("Orchestration flow not found for InvoiceId={InvoiceId}", invoiceId);
            return NotFound();
        }
        _logger.LogInformation("Orchestration flow found for InvoiceId={InvoiceId}, State={State}", invoiceId, flow.CurrentState);
        return Ok(Map(flow));
    }

    private static ProcessFlowDto Map(ProcessFlow flow) =>
        new(
            flow.Id,
            flow.InvoiceId,
            flow.FlowType,
            flow.CurrentState,
            flow.LastPaymentId,
            flow.StartedAtUtc,
            flow.UpdatedAtUtc,
            flow.Steps
                .OrderBy(s => s.OccurredAtUtc)
                .Select(s => new FlowStepDto(s.Id, s.StepType, s.PayloadJson, s.OccurredAtUtc))
                .ToList());
}
