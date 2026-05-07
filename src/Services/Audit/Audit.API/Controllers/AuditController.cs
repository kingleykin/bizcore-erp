using Asp.Versioning;
using Audit.API.Application.DTOs;
using Audit.API.Application.Services;
using Audit.API.Infrastructure.Data;
using Bizcore.BuildingBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Audit.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/audit")]
    [Authorize]
    public class AuditController : ControllerBase
    {
        private readonly IAuditQueryService _queryService;
        private readonly AuditDbContext     _db;

        public AuditController(IAuditQueryService queryService, AuditDbContext db)
        {
            _queryService = queryService;
            _db           = db;
        }

        /// <summary>
        /// Query audit entries with full filtering and pagination.
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "Audit.View")]
        public async Task<IActionResult> Query([FromQuery] AuditQueryParams q, CancellationToken ct)
        {
            var result = await _queryService.QueryAsync(q, ct);
            return Ok(result);
        }

        /// <summary>
        /// Get a single audit entry by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "Audit.View")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var entry = await _queryService.GetByIdAsync(id, ct);
            return entry is null ? NotFound(new { error = $"Audit entry '{id}' not found." }) : Ok(entry);
        }

        /// <summary>
        /// Verify the SHA-256 hash chain integrity of the hot audit store.
        /// Returns whether the chain is intact or has been tampered with.
        /// </summary>
        [HttpGet("verify-integrity")]
        [Authorize(Policy = "Audit.View")]
        public async Task<IActionResult> VerifyIntegrity(CancellationToken ct)
        {
            var result = await _queryService.VerifyIntegrityAsync(ct);
            return result.IsValid
                ? Ok(result)
                : StatusCode(500, result);
        }

        /// <summary>
        /// Được gọi bởi Source Service sau khi reversal thành công.
        /// Đánh dấu AuditEntry gốc là đã được reverse để tránh reverse lại.
        /// </summary>
        [HttpPatch("{id:guid}/mark-reversed")]
        [Authorize(Policy = "Audit.View")]
        public async Task<IActionResult> MarkReversed(
            Guid id,
            [FromBody] MarkReversedRequest request,
            CancellationToken ct)
        {
            var entry = await _db.AuditEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entry is null) return NotFound();

            try
            {
                entry.MarkAsReversed(request.ReversalEntryId, request.Reason);
                await _db.SaveChangesAsync(ct);
                return Ok(new { message = "AuditEntry đã được đánh dấu reversed." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }
    }

    public record MarkReversedRequest(Guid ReversalEntryId, string Reason);
}
