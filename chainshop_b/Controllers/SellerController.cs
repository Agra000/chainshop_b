using chainshop_b.Model.Dto.Request;
using chainshop_b.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SellerController : ControllerBase
    {
        private readonly SellerService _sellerService;

        public SellerController(SellerService sellerService)
        {
            _sellerService = sellerService;
        }

        [HttpPost("become-seller")]
        public async Task<IActionResult> BecomeSeller([FromQuery] Guid userId, [FromBody] BecomeSellerRequest req)
        {
            var res = await _sellerService.BecomeSeller(userId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [AllowAnonymous]
        [HttpPost("get-info")]
        public async Task<IActionResult> GetSellerInfo([FromQuery] Guid sellerId)
        {
            var res = await _sellerService.GetSellerInfo(sellerId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                shopName = res.shopName,
                shopDescription = res.shopDescription,
                city = res.city,
                payoutWallet = res.payoutWallet,
                registeredAt = res.registeredAt,
                shopImage = res.shopImage
            });
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetSellerDashboard([FromQuery] Guid sellerId)
        {
            var res = await _sellerService.GetSellerDashboard(sellerId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                shopName = res.shopName,
                ratingAvg = res.ratingAvg,
                ratingCount = res.ratingCount,
                totalProducts = res.totalProducts,
                totalSold = res.totalSold,
                totalOrders = res.totalOrders,
                newOrders = res.newOrders,
                onShippingOrders = res.onShippingOrders,
                completedOrders = res.completedOrders,
                escrowHeldIdr = res.escrowHeldIdr,
                totalRevenueIdr = res.totalRevenueIdr,
                products = res.products
            });
        }
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteStore([FromQuery] Guid sellerId)
        {
            var res = await _sellerService.DeleteStore(sellerId);
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
