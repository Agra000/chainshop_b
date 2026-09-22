using chainshop_b.Services;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class CartController : ControllerBase
    {
        private readonly CartService _cartService;

        public CartController(CartService cartService)
        {
            _cartService = cartService;
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllCarts()
        {
            var res = await _cartService.GetAllCarts();
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpPost("upsert")]
        public async Task<IActionResult> UpsertToCart([FromQuery] Guid userId, [FromBody] Guid productId)
        {
            var res = await _cartService.UpsertToCart(userId, productId);

            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

    }
}
