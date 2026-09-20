//using chainshop_b.Services;
//using Microsoft.AspNetCore.Mvc;

//namespace chainshop_b.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    //[Authorize]
//    public class CartController : ControllerBase
//    {
//        private readonly CartService _cartService;

//        public CartController(CartService cartService)
//        {
//            _cartService = cartService;
//        }

//        [HttpGet("get-all")]
//        public async Task<IActionResult> GetAllProducts()
//        {
//            var res = await _cartService.GetAllProducts();
//            return Ok(new
//            {
//                data = res,
//                message = "Success"
//            });
//        }

//        [HttpPost("add/{userId}")]
//        public async Task<IActionResult> UpsertToCart([FromRoute] Guid userId, [FromBody] Guid trItemId)
//        {
//            var res = await _cartService.UpsertToCart(userId, trItemId);

//            return Ok(new
//            {
//                status = res.Status,
//                message = res.Message
//            });
//        }

//    }
//}
