using chainshop_b.Model.Dto.Request;
using chainshop_b.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReviewController : ControllerBase
    {
        private readonly ReviewService _reviewService;

        public ReviewController(ReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddReview([FromQuery] Guid userId, [FromBody] ReviewRequest req)
        {
            var res = await _reviewService.AddReview(userId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [AllowAnonymous]
        [HttpGet("get-by-product")]
        public async Task<IActionResult> GetProductReviews([FromQuery] Guid productId)
        {
            var res = await _reviewService.GetProductReviews(productId);
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }
    }
}
