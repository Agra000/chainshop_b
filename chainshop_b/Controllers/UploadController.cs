using chainshop_b.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly StorageService _storageService;

        public UploadController(StorageService storageService)
        {
            _storageService = storageService;
        }

        // multipart/form-data, field "file". folder: products | shops | avatars
        [HttpPost("image")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage([FromQuery] Guid userId, [FromQuery] string folder, IFormFile file)
        {
            var res = await _storageService.UploadImage(userId, folder, file);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                url = res.url,
                path = res.path
            });
        }
    }
}
