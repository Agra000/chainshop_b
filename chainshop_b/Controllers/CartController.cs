using chainshop_b.Model.Dto.Request;
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

        [HttpGet("get-by-user")]
        public async Task<IActionResult> GetCartByUser([FromQuery] Guid userId)
        {
            var res = await _cartService.GetCartByUser(userId);
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateCartItem([FromQuery] Guid userId, [FromBody] UpdateCartRequest req)
        {
            var res = await _cartService.UpdateCartItem(userId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpPut("select")]
        public async Task<IActionResult> SelectCartItem([FromQuery] Guid userId, [FromQuery] Guid cartId, [FromQuery] bool isSelected)
        {
            var res = await _cartService.SelectCartItem(userId, cartId, isSelected);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpDelete("remove")]
        public async Task<IActionResult> RemoveFromCart([FromQuery] Guid userId, [FromQuery] Guid cartId)
        {
            var res = await _cartService.RemoveFromCart(userId, cartId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }
    }
}
