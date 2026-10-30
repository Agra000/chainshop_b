using chainshop_b.Model.Dto.Request;
using chainshop_b.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly EscrowAutomationRunner _automation;

        public OrderController(OrderService orderService, EscrowAutomationRunner automation)
        {
            _orderService = orderService;
            _automation = automation;
        }

        // ===== Buyer =====

        // rincian per seller (harga barang, ongkir, estimasi gas ±) sebelum bayar
        [HttpGet("checkout-preview")]
        public async Task<IActionResult> GetCheckoutPreview([FromQuery] Guid userId)
        {
            var res = await _orderService.GetCheckoutPreview(userId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                grandTotal = res.grandTotal,
                totalGasFee = res.totalGasFee,
                escrowAddress = res.escrowAddress,
                data = res.groups
            });
        }

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
        public async Task<IActionResult> ConfirmDelivery([FromQuery] Guid userId, [FromQuery] Guid orderId, [FromBody] AcceptOrderRequest req)
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

        // generate nomor resi + invoice id, kembalikan data invoice untuk markShipped onchain
        [HttpPost("prepare-shipment")]
        public async Task<IActionResult> PrepareShipment([FromQuery] Guid sellerId, [FromQuery] Guid orderId, [FromBody] PrepareShipmentRequest req)
        {
            var res = await _orderService.PrepareShipment(sellerId, orderId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                data = res.Status ? res : null
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

        // v3: seller klaim dana (releaseWithAcceptance dari wallet seller) setelah buyer menandatangani persetujuan
        [HttpPost("claim-payment")]
        public async Task<IActionResult> ClaimPayment([FromQuery] Guid sellerId, [FromQuery] Guid orderId, [FromBody] ClaimPaymentRequest req)
        {
            var res = await _orderService.ClaimPayment(sellerId, orderId, req);
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

        // ===== Buyer / Seller / Admin =====

        // sinkronkan status order dengan smart contract setelah aksi onchain
        // (markDelivered, retur, confirmReturnReceived, auto*). txHash opsional.
        [HttpPost("sync")]
        public async Task<IActionResult> SyncOrder([FromQuery] Guid orderId, [FromBody] SyncOrderRequest? req)
        {
            var claim = User.FindFirst("iduser")?.Value;
            if (!Guid.TryParse(claim, out var loginUserId))
            {
                return Unauthorized(new { status = false, message = "Session expired, please login again" });
            }

            var res = await _orderService.SyncOrder(loginUserId, orderId, req?.TxHash);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        // jalankan aksi otomatis yang sudah jatuh tempo sekarang juga (tombol "... now"), gas dibayar wallet operator
        [HttpPost("run-auto")]
        public async Task<IActionResult> RunAuto([FromQuery] Guid orderId)
        {
            var claim = User.FindFirst("iduser")?.Value;
            if (!Guid.TryParse(claim, out var loginUserId))
            {
                return Unauthorized(new { status = false, message = "Session expired, please login again" });
            }

            if (!await _orderService.CanAccessOrder(loginUserId, orderId))
            {
                return Ok(new { status = false, message = "Order not found" });
            }

            (bool ok, string message) res;
            try
            {
                res = await _automation.Run(orderId);
            }
            catch (Exception)
            {
                res = (false, "Could not reach the blockchain right now, please try again");
            }
            return Ok(new
            {
                status = res.ok,
                message = res.message
            });
        }

        // ===== Public =====

        [AllowAnonymous]
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
                invoiceTxUrl = res.invoiceTxUrl,
                isMatch = res.isMatch
            });
        }
    }
}
