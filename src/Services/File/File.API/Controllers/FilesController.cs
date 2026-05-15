using File.API.Application.Commands;
using File.API.Application.Queries;
using File.API.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Bizcore.BuildingBlocks.Storage;

namespace File.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IStorageService _storageService;

    public FilesController(IMediator mediator, IStorageService storageService)
    {
        _mediator = mediator;
        _storageService = storageService;
    }

    [HttpPost("upload")]
    [ProducesResponseType(typeof(FileUploadResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] bool isPublic = false, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        using var stream = file.OpenReadStream();
        var command = new UploadFileCommand(stream, file.FileName, file.ContentType, isPublic);
        var result = await _mediator.Send(command, ct);
        
        return Ok(result);
    }

    [HttpGet("download/{fileName}")]
    public async Task<IActionResult> Download(string fileName, [FromQuery] bool isPublic = false, CancellationToken ct = default)
    {
        var stream = await _storageService.DownloadAsync(fileName, isPublic, ct);
        return File(stream, "application/octet-stream", fileName);
    }

    [HttpGet("view-url/{fileName}")]
    [ProducesResponseType(typeof(FileUrlResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetViewUrl(string fileName, [FromQuery] bool isPublic = false, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFileUrlQuery(fileName, isPublic), ct);
        return Ok(result);
    }

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> Delete(string fileName, [FromQuery] bool isPublic = false, CancellationToken ct = default)
    {
        await _mediator.Send(new DeleteFileCommand(fileName, isPublic), ct);
        return NoContent();
    }
}
