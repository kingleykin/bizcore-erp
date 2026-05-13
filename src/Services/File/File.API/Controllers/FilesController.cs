using Bizcore.BuildingBlocks.Storage;
using Microsoft.AspNetCore.Mvc;

namespace File.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private readonly ILogger<FilesController> _logger;

        public FilesController(IStorageService storageService, ILogger<FilesController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            try
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                using var stream = file.OpenReadStream();
                
                var result = await _storageService.UploadAsync(stream, fileName, file.ContentType, cancellationToken);
                
                return Ok(new { FileName = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, "Internal server error during upload.");
            }
        }

        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> Download(string fileName, CancellationToken cancellationToken)
        {
            try
            {
                var stream = await _storageService.DownloadAsync(fileName, cancellationToken);
                return File(stream, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FileName}", fileName);
                return NotFound();
            }
        }

        [HttpGet("view-url/{fileName}")]
        public async Task<IActionResult> GetViewUrl(string fileName, CancellationToken cancellationToken)
        {
            try
            {
                var url = await _storageService.GetPresignedUrlAsync(fileName, 3600, cancellationToken);
                return Ok(new { Url = url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting view url for {FileName}", fileName);
                return NotFound();
            }
        }

        [HttpDelete("{fileName}")]
        public async Task<IActionResult> Delete(string fileName, CancellationToken cancellationToken)
        {
            try
            {
                await _storageService.DeleteAsync(fileName, cancellationToken);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FileName}", fileName);
                return StatusCode(500, "Internal server error during deletion.");
            }
        }
    }
}
