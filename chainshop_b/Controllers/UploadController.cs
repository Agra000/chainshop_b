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

        // ganti gambar: upload file baru lalu hapus file lama (oldUrl)
        [HttpPut("image")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> ReplaceImage([FromQuery] Guid userId, [FromQuery] string folder, [FromQuery] string? oldUrl, IFormFile file)
        {
            var res = await _storageService.ReplaceImage(userId, folder, file, oldUrl);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                url = res.url,
                path = res.path
            });
        }

        // hapus gambar milik user (url publik atau path di bucket)
        [HttpDelete("image")]
        public async Task<IActionResult> DeleteImage([FromQuery] Guid userId, [FromQuery] string url)
        {
            var res = await _storageService.DeleteImage(userId, url);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                path = res.path
            });
        }
    }
}
