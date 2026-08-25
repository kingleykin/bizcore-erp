using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using Product.API.Application.DTOs;
using Product.API.Application.Commands;
using Product.API.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace Product.API.Controllers;

/// <summary>
/// Quản lý Sản phẩm (Product) trong danh mục bán hàng.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission(Permissions.Product.View)]
    [ProducesResponseType(typeof(IEnumerable<ProductResponseDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive = null)
    {
        var result = await _mediator.Send(new GetProductsQuery(isActive));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Product.View)]
    [ProducesResponseType(typeof(ProductResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(id));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.Product.Create)]
    [ProducesResponseType(typeof(ProductResponseDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var result = await _mediator.Send(new CreateProductCommand(request));
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Product.Update)]
    [ProducesResponseType(typeof(ProductResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request)
    {
        var result = await _mediator.Send(new UpdateProductCommand(id, request));
        return Ok(result);
    }

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(Permissions.Product.Update)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var success = await _mediator.Send(new DeactivateProductCommand(id));
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(Permissions.Product.Update)]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Activate(Guid id)
    {
        var success = await _mediator.Send(new ActivateProductCommand(id));
        return success ? NoContent() : NotFound();
    }
}
