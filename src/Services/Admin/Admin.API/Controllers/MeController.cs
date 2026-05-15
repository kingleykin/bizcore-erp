using Asp.Versioning;
using Admin.API.Application.DTOs;
using Admin.API.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/me")]
    [ApiVersion("1.0")]
    [Authorize]
    public class MeController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MeController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("permissions")]
        [ProducesResponseType(typeof(UserPermissionsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyPermissions(CancellationToken ct)
        {
            var query = new GetMyPermissionsQuery(User);
            var result = await _mediator.Send(query, ct);

            return result == null ? Unauthorized() : Ok(result);
        }

        [HttpGet("navigation")]
        [ProducesResponseType(typeof(NavigationMenuDto[]), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyNavigation(CancellationToken ct)
        {
            var query = new GetMyNavigationQuery(User);
            var result = await _mediator.Send(query, ct);

            return result == null ? Unauthorized() : Ok(result);
        }
    }
}
