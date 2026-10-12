using chainshop_b.Model.Dto.Request;
using chainshop_b.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductController : ControllerBase
    {
        private readonly ProductService _productService;

        public ProductController(ProductService productService)
        {
            _productService = productService;
        }

        [AllowAnonymous]
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllProducts()
        {
            var res = await _productService.GetAllProducts();
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpPost("upsert")]
        public async Task<IActionResult> UpsertProduct([FromQuery] Guid sellerId, [FromBody] FormItemsRequest req)
        {
            var res = await _productService.UpsertProduct(sellerId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [AllowAnonymous]
        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts([FromQuery] string? keyword, [FromQuery] Guid? categoryId, [FromQuery] string? categorySlug,
            [FromQuery] long? minPrice, [FromQuery] long? maxPrice, [FromQuery] string? sortBy)
        {
            var res = await _productService.SearchProducts(keyword, categoryId, categorySlug, minPrice, maxPrice, sortBy);
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [AllowAnonymous]
        [HttpGet("detail")]
        public async Task<IActionResult> GetProductDetail([FromQuery] Guid productId)
        {
            var res = await _productService.GetProductDetail(productId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                data = res.product,
                sellerId = res.sellerId,
                sellerCity = res.sellerCity,
                sellerRating = res.sellerRating,
                ratingCount = res.ratingCount,
                specifications = res.specifications
            });
        }

        [AllowAnonymous]
        [HttpGet("get-by-seller")]
        public async Task<IActionResult> GetProductsBySeller([FromQuery] Guid sellerId)
        {
            var res = await _productService.GetProductsBySeller(sellerId);
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateProduct([FromQuery] Guid sellerId, [FromQuery] Guid productId, [FromBody] FormItemsRequest req)
        {
            var res = await _productService.UpdateProduct(sellerId, productId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteProduct([FromQuery] Guid sellerId, [FromQuery] Guid productId)
        {
            var res = await _productService.DeleteProduct(sellerId, productId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        //[HttpGet("get-edit")]
        //public async Task<IActionResult> GetEdit([FromQuery] Guid taskId)
        //{
        //    var res = await _taskService.GetEditTask(taskId);

        //    if (res == null)
        //    {
        //        return Ok(new
        //        {
        //            data = new List<object>(),
        //            message = "Tasks not found !"
        //        });
        //    }

        //    return Ok(new
        //    {
        //        data = res,
        //        message = "Success"
        //    });
        //}

        //[HttpPut("update")]
        //public async Task<IActionResult> Update([FromQuery] Guid userId, [FromQuery] Guid taskId, [FromBody] TaskRequest req)
        //{
        //    var res = await _taskService.EditTask(userId, taskId, req);

        //    if (!res.Status)
        //    {
        //        return BadRequest(new
        //        {
        //            status = res.Status,
        //            message = res.Message
        //        });
        //    }
        //    else
        //    {
        //        return Ok(new
        //        {
        //            status = res.Status,
        //            message = res.Message
        //        });
        //    }
        //}
    }
}
