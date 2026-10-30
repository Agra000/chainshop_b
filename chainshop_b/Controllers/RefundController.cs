using chainshop_b.Filters;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RefundController : ControllerBase
    {
        private readonly RefundService _refundService;

        public RefundController(RefundService refundService)
        {
            _refundService = refundService;
        }

        // ===== Buyer =====

        // cek kelayakan + validasi tx id invoice sebelum buyer mengirim form refund (off-chain)
        [HttpPost("check")]
        public async Task<IActionResult> CheckRefund([FromQuery] Guid userId, [FromQuery] Guid orderId, [FromBody] RefundCheckRequest req)
        {
            var res = await _refundService.CheckRefund(userId, orderId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                notDelivered = res.notDelivered,
                attempt = res.attempt
            });
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestRefund([FromQuery] Guid userId, [FromQuery] Guid orderId, [FromBody] RefundRequest req)
        {
            var res = await _refundService.RequestRefund(userId, orderId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        // buyer sudah mengirim barang retur (off-chain)
        [HttpPost("return-shipped")]
        public async Task<IActionResult> ReturnShipped([FromQuery] Guid userId, [FromQuery] Guid orderId)
        {
            var res = await _refundService.ReturnShipped(userId, orderId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        // barang retur sudah sampai di seller (off-chain), timer seller untuk konfirmasi dimulai
        [HttpPost("return-delivered")]
        public async Task<IActionResult> ReturnDelivered([FromQuery] Guid userId, [FromQuery] Guid orderId)
        {
            var res = await _refundService.ReturnDelivered(userId, orderId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        // ===== Admin (= arbiter) =====

        [HttpGet("admin-list")]
        [ServiceFilter(typeof(AdminAccessFilter))]
        public async Task<IActionResult> GetAdminRefunds([FromQuery] Guid userId, [FromQuery] string? status)
        {
            var res = await _refundService.GetAdminRefunds(status);
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpGet("admin-detail")]
        [ServiceFilter(typeof(AdminAccessFilter))]
        public async Task<IActionResult> GetRefundDetail([FromQuery] Guid userId, [FromQuery] Guid refundId)
        {
            var res = await _refundService.GetRefundDetail(refundId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                refund = res.refund,
                order = res.order
            });
        }

        [HttpPost("admin-decide")]
        [ServiceFilter(typeof(AdminAccessFilter))]
        public async Task<IActionResult> DecideRefund([FromQuery] Guid userId, [FromQuery] Guid refundId, [FromBody] RefundDecisionRequest req)
        {
            var res = await _refundService.DecideRefund(userId, refundId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }
    }
}
