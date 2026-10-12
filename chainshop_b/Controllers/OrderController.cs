using chainshop_b.Model.Dto.Request;
using chainshop_b.Services;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class OrderController : ControllerBase
    {
        private readonly OrderService _orderService;

        public OrderController(OrderService orderService)
        {
            _orderService = orderService;
        }

        // ===== Buyer =====

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromQuery] Guid userId, [FromBody] CheckoutRequest req)
        {
            var res = await _orderService.Checkout(userId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                grandTotal = res.grandTotal,
                data = res.orders
            });
        }

        [HttpPost("pay")]
        public async Task<IActionResult> PayOrder([FromQuery] Guid userId, [FromQuery] Guid orderId, [FromBody] EscrowTxRequest req)
        {
            var res = await _orderService.PayOrder(userId, orderId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpPost("cancel")]
        public async Task<IActionResult> CancelOrder([FromQuery] Guid userId, [FromQuery] Guid orderId)
        {
            var res = await _orderService.CancelOrder(userId, orderId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpGet("my-orders")]
        public async Task<IActionResult> GetBuyerOrders([FromQuery] Guid userId, [FromQuery] string? status)
        {
            var res = await _orderService.GetBuyerOrders(userId, status);
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpPost("confirm-delivery")]
        public async Task<IActionResult> ConfirmDelivery([FromQuery] Guid userId, [FromQuery] Guid orderId, [FromBody] EscrowTxRequest req)
        {
            var res = await _orderService.ConfirmDelivery(userId, orderId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        // detail order + tracking delivery (isi userId untuk buyer atau sellerId untuk seller)
        [HttpGet("detail")]
        public async Task<IActionResult> GetOrderDetail([FromQuery] Guid orderId, [FromQuery] Guid? userId, [FromQuery] Guid? sellerId)
        {
            var res = await _orderService.GetOrderDetail(orderId, userId, sellerId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                data = res.Status ? res : null
            });
        }

        // ===== Seller =====

        [HttpGet("seller-orders")]
        public async Task<IActionResult> GetSellerOrders([FromQuery] Guid sellerId, [FromQuery] string? status)
        {
            var res = await _orderService.GetSellerOrders(sellerId, status);
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpPost("process")]
        public async Task<IActionResult> ProcessOrder([FromQuery] Guid sellerId, [FromQuery] Guid orderId)
        {
            var res = await _orderService.ProcessOrder(sellerId, orderId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpPost("reject")]
        public async Task<IActionResult> RejectOrder([FromQuery] Guid sellerId, [FromQuery] Guid orderId, [FromBody] EscrowTxRequest req)
        {
            var res = await _orderService.RejectOrder(sellerId, orderId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpGet("shipping-invoice")]
        public async Task<IActionResult> GetShippingInvoice([FromQuery] Guid sellerId, [FromQuery] Guid orderId, [FromQuery] Guid driverId, [FromQuery] string trackingNumber)
        {
            var res = await _orderService.GetShippingInvoice(sellerId, orderId, driverId, trackingNumber);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                orderCode = res.orderCode,
                invoicePayload = res.invoicePayload,
                invoiceHash = res.invoiceHash
            });
        }

        [HttpPost("ship")]
        public async Task<IActionResult> ShipOrder([FromQuery] Guid sellerId, [FromQuery] Guid orderId, [FromBody] ShipOrderRequest req)
        {
            var res = await _orderService.ShipOrder(sellerId, orderId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpPut("update-shipping")]
        public async Task<IActionResult> UpdateShippingStatus([FromQuery] Guid sellerId, [FromQuery] Guid orderId, [FromBody] UpdateShippingRequest req)
        {
            var res = await _orderService.UpdateShippingStatus(sellerId, orderId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        // ===== Public =====

        [HttpGet("verify-invoice")]
        public async Task<IActionResult> VerifyInvoice([FromQuery] Guid orderId, [FromQuery] string? invoiceHash)
        {
            var res = await _orderService.VerifyInvoice(orderId, invoiceHash);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                orderCode = res.orderCode,
                invoicePayload = res.invoicePayload,
                invoiceHash = res.invoiceHash,
                invoiceTxHash = res.invoiceTxHash,
                isMatch = res.isMatch
            });
        }
    }
}
