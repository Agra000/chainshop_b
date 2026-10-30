using chainshop_b.Blockchain;
using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    // Refund ala marketplace, seluruh prosesnya OFF-CHAIN (tanpa gas):
    //   buyer ajukan (maks 3x, alasan + foto + tx id invoice) -> admin (= arbiter) setujui / tolak
    //   -> buyer kirim retur -> retur sampai -> seller konfirmasi.
    // Hanya keputusan akhir yang memindahkan dana yang menjadi transaksi onchain:
    //   refund (seller konfirmasi retur / admin untuk barang tak sampai / cron) atau release (cron, retur tidak dikirim).
    public class RefundService
    {
        private readonly ApplicationDBContext _context;
        private readonly BlockchainService _chain;
        private readonly EscrowSyncService _sync;
        private readonly OrderService _orderService;

        private const string StatusShipped = EscrowSyncService.StatusShipped;
        private const string StatusInTransit = EscrowSyncService.StatusInTransit;
        private const string StatusDelivered = EscrowSyncService.StatusDelivered;
        private const string StatusRefundRequested = EscrowSyncService.StatusRefundRequested;
        private const string StatusReturnPending = EscrowSyncService.StatusReturnPending;
        private const string StatusReturnShipped = EscrowSyncService.StatusReturnShipped;
        private const string StatusReturnDelivered = EscrowSyncService.StatusReturnDelivered;

        public RefundService(ApplicationDBContext context, BlockchainService chain, EscrowSyncService sync, OrderService orderService)
        {
            _context = context;
            _chain = chain;
            _sync = sync;
            _orderService = orderService;
        }

        private static string FormatDate(DateTime? date) => date == null ? "" : date.Value.ToString("yyyy-MM-dd HH:mm:ss");
        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "…";
        private static ResultMessageResponse Fail(string message) => new() { Status = false, Message = message };
        private static ResultMessageResponse Ok(string message) => new() { Status = true, Message = message };

        private async Task<(string? error, bool notDelivered)> CheckEligibility(TrOrders order, string? invoiceTxHash)
        {
            if (order.RefundCount >= _chain.MaxRefundRequests)
            {
                return ($"You have reached the maximum of {_chain.MaxRefundRequests} refund requests for this order", false);
            }

            bool notDelivered;
            if (order.Status == StatusDelivered)
            {
                if (order.BuyerDeadlineAt != null && order.BuyerDeadlineAt < DateTime.UtcNow)
                {
                    return ("The window to request a refund has passed", false);
                }
                notDelivered = false;
            }
            else if (order.Status == StatusShipped || order.Status == StatusInTransit)
            {
                var availableAt = order.ShippedAt?.Add(_chain.NotDeliveredWindow);
                if (availableAt == null || availableAt > DateTime.UtcNow)
                {
                    return ($"You can request a refund for an undelivered package after {FormatDate(availableAt)} UTC", false);
                }
                notDelivered = true;
            }
            else
            {
                return ("Refund can only be requested after the package arrives (or if it has not arrived long after shipping)", false);
            }

            if (string.IsNullOrWhiteSpace(order.InvoiceTxHash) || string.IsNullOrWhiteSpace(invoiceTxHash) ||
                !string.Equals(invoiceTxHash.Trim(), order.InvoiceTxHash, StringComparison.OrdinalIgnoreCase))
            {
                return ("Invoice transaction ID does not match this order", notDelivered);
            }

            var verify = await _chain.VerifyEscrowTx(order.InvoiceTxHash, order.OnchainOrderId!, null, EscrowEvents.InvoiceRecorded);
            if (!verify.ok) return ($"Invoice transaction is not valid on-chain: {verify.message}", notDelivered);

            return (null, notDelivered);
        }

        // cek kelayakan + validasi tx id invoice sebelum buyer mengirim form refund
        public async Task<RefundCheckResponse> CheckRefund(Guid userId, Guid orderId, RefundCheckRequest req)
        {
            try
            {
                if (!_chain.IsEnabled) return new RefundCheckResponse { Status = false, Message = _chain.NotConfiguredMessage };

                var order = await _context.TrOrders.FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);
                if (order == null) return new RefundCheckResponse { Status = false, Message = "Order not found" };

                var (error, notDelivered) = await CheckEligibility(order, req.InvoiceTxHash);
                return new RefundCheckResponse
                {
                    Status = error == null,
                    Message = error ?? "Invoice verified, you can submit the refund request",
                    notDelivered = notDelivered,
                    attempt = order.RefundCount + 1
                };
            }
            catch (Exception ex)
            {
                return new RefundCheckResponse { Status = false, Message = "Server error occurred while checking refund" };
            }
        }

        // ===== Buyer (off-chain) =====

        public async Task<ResultMessageResponse> RequestRefund(Guid userId, Guid orderId, RefundRequest req)
        {
            try
            {
                var order = await _context.TrOrders.FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);
                if (order == null) return Fail("Order not found");
                if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Trim().Length < 10)
                {
                    return Fail("Please describe the reason (at least 10 characters)");
                }

                var (error, notDelivered) = await CheckEligibility(order, req.InvoiceTxHash);
                if (error != null) return Fail(error);

                var now = DateTime.UtcNow;
                var reason = req.Reason.Trim();

                order.RefundCount += 1;
                order.StatusBeforeRefund = order.Status;
                // timer buyer di-pause: simpan sisa waktunya
                order.BuyerRemainingSeconds = order.BuyerDeadlineAt == null
                    ? null
                    : (int)Math.Max(0, (order.BuyerDeadlineAt.Value - now).TotalSeconds);
                order.BuyerDeadlineAt = null;
                order.Status = StatusRefundRequested;
                order.UpdatedAt = now;
                order.UpdatedBy = userId;

                _context.TrRefundRequests.Add(new TrRefundRequests
                {
                    OrderId = order.Id,
                    BuyerId = userId,
                    Attempt = order.RefundCount,
                    Reason = reason,
                    PhotoUrl = string.IsNullOrWhiteSpace(req.PhotoUrl) ? null : req.PhotoUrl.Trim(),
                    InvoiceTxHash = order.InvoiceTxHash!,
                    NotDelivered = notDelivered,
                    Status = "pending",
                    CreatedAt = now,
                    UpdatedAt = now,
                    UpdatedBy = userId
                });

                _sync.AddTracking(order.Id, StatusRefundRequested,
                    $"Buyer requested a refund (attempt {order.RefundCount} of {_chain.MaxRefundRequests}): {Truncate(reason, 120)}",
                    null, userId);

                await _context.SaveChangesAsync();
                return Ok("Refund request submitted, waiting for admin review");
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while requesting refund");
            }
        }

        // buyer sudah menyerahkan barang retur ke kurir
        public async Task<ResultMessageResponse> ReturnShipped(Guid userId, Guid orderId)
        {
            try
            {
                var order = await _context.TrOrders.Include(x => x.Address)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);
                if (order == null) return Fail("Order not found");
                if (order.Status != StatusReturnPending) return Fail("This order is not waiting for a return");
                if (order.ReturnDeadlineAt != null && order.ReturnDeadlineAt < DateTime.UtcNow)
                {
                    return Fail("The return deadline has passed, the refund is cancelled");
                }

                order.Status = StatusReturnShipped;
                order.ReturnTrackingNumber ??= EscrowSyncService.GenerateTrackingNumber("RTN");
                order.ReturnDeadlineAt = null;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = userId;

                _sync.AddTracking(order.Id, StatusReturnShipped,
                    $"Buyer shipped the item back (return tracking {order.ReturnTrackingNumber}), estimated arrival: tomorrow",
                    order.Address?.City, userId);

                await _context.SaveChangesAsync();
                return Ok("Return shipment recorded");
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while recording the return");
            }
        }

        // barang retur sudah sampai di seller -> timer seller untuk konfirmasi
        public async Task<ResultMessageResponse> ReturnDelivered(Guid userId, Guid orderId)
        {
            try
            {
                var order = await _context.TrOrders.Include(x => x.Seller)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);
                if (order == null) return Fail("Order not found");
                if (order.Status != StatusReturnShipped) return Fail("The return has not been shipped yet");

                order.Status = StatusReturnDelivered;
                order.ReturnDeadlineAt = DateTime.UtcNow.Add(_chain.ReturnConfirmWindow);
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = userId;

                _sync.AddTracking(order.Id, StatusReturnDelivered,
                    "Returned item arrived at seller, waiting for seller confirmation", order.Seller?.City, userId);

                await _context.SaveChangesAsync();
                return Ok("Return marked as arrived");
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while recording the return");
            }
        }

        // ===== Admin (= arbiter) =====

        public async Task<List<GetRefundResponse>> GetAdminRefunds(string? status)
        {
            try
            {
                var query = _context.TrRefundRequests
                    .Include(x => x.Order).ThenInclude(x => x!.Buyer)
                    .Include(x => x.Order).ThenInclude(x => x!.Seller)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

                var list = await query
                    .OrderBy(x => x.Status == "pending" ? 0 : 1)
                    .ThenByDescending(x => x.CreatedAt)
                    .Take(200)
                    .ToListAsync();

                return list.Select(OrderService.MapRefund).ToList();
            }
            catch (Exception ex)
            {
                return new List<GetRefundResponse>();
            }
        }

        public async Task<GetRefundDetailResponse> GetRefundDetail(Guid refundId)
        {
            try
            {
                var refund = await _context.TrRefundRequests
                    .Include(x => x.Order).ThenInclude(x => x!.Buyer)
                    .Include(x => x.Order).ThenInclude(x => x!.Seller)
                    .FirstOrDefaultAsync(x => x.Id == refundId);

                if (refund == null) return new GetRefundDetailResponse { Status = false, Message = "Refund request not found" };

                var order = await _orderService.GetOrderDetail(refund.OrderId, null, null, adminView: true);
                return new GetRefundDetailResponse
                {
                    Status = true,
                    Message = "Success",
                    refund = OrderService.MapRefund(refund),
                    order = order.Status ? order : null
                };
            }
            catch (Exception ex)
            {
                return new GetRefundDetailResponse { Status = false, Message = "Server error occurred while getting refund detail" };
            }
        }

        // keputusan admin:
        // - tolak: off-chain, order kembali ke status sebelumnya dan timer buyer lanjut dari sisa waktu
        // - setujui (barang sudah sampai): off-chain, buyer wajib kirim retur
        // - setujui (barang tak sampai): admin memanggil refund() onchain lalu mengirim txHash-nya
        public async Task<ResultMessageResponse> DecideRefund(Guid adminId, Guid refundId, RefundDecisionRequest req)
        {
            try
            {
                var refund = await _context.TrRefundRequests
                    .Include(x => x.Order).ThenInclude(x => x!.Buyer)
                    .Include(x => x.Order).ThenInclude(x => x!.Seller)
                    .FirstOrDefaultAsync(x => x.Id == refundId);

                if (refund == null || refund.Order == null) return Fail("Refund request not found");
                if (refund.Status != "pending") return Fail("This refund request has already been decided");

                var order = refund.Order;
                if (order.Status != StatusRefundRequested) return Fail("The order is no longer waiting for a refund decision");

                var now = DateTime.UtcNow;
                var adminNote = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
                refund.AdminNote = adminNote;
                refund.DecidedBy = adminId;
                refund.DecidedAt = now;
                refund.UpdatedAt = now;
                refund.UpdatedBy = adminId;

                if (!req.Approve)
                {
                    refund.Status = "rejected";
                    var previous = order.StatusBeforeRefund ?? StatusDelivered;
                    order.Status = previous;
                    order.StatusBeforeRefund = null;
                    if (previous == StatusDelivered)
                    {
                        // timer buyer lanjut dari sisa waktu sebelum refund diajukan
                        var remaining = order.BuyerRemainingSeconds ?? (int)_chain.BuyerConfirmWindow.TotalSeconds;
                        order.BuyerDeadlineAt = now.AddSeconds(remaining);
                    }
                    order.BuyerRemainingSeconds = null;
                    order.UpdatedAt = now;
                    order.UpdatedBy = adminId;

                    _sync.AddTracking(order.Id, previous,
                        "Refund request rejected by admin" + (adminNote == null ? "" : $": {adminNote}"), null, adminId);
                    await _context.SaveChangesAsync();
                    return Ok("Refund rejected");
                }

                if (!refund.NotDelivered)
                {
                    refund.Status = "approved";
                    order.Status = StatusReturnPending;
                    order.StatusBeforeRefund = null;
                    order.BuyerRemainingSeconds = null;
                    order.ReturnDeadlineAt = now.Add(_chain.ReturnShipWindow);
                    order.UpdatedAt = now;
                    order.UpdatedBy = adminId;

                    _sync.AddTracking(order.Id, StatusReturnPending,
                        "Refund approved by admin, buyer must ship the item back" + (adminNote == null ? "" : $": {adminNote}"),
                        null, adminId);
                    await _context.SaveChangesAsync();
                    return Ok("Refund approved, waiting for the buyer to return the item");
                }

                // barang tak sampai: dana langsung dikembalikan onchain oleh admin
                if (!BlockchainService.IsValidTxHash(req.TxHash))
                {
                    // retry: refund sudah dikirim onchain tapi penyimpanan keputusan sebelumnya gagal
                    var current = await _chain.GetOrder(order.OnchainOrderId!);
                    if (current?.Status == OnchainStatus.Refunded)
                    {
                        refund.Status = "approved";
                        var resync = await _sync.SyncOrder(order, null, adminId,
                            "Refund approved by admin (package never arrived), payment returned to buyer" +
                            (adminNote == null ? "" : $": {adminNote}"));
                        return resync.ok ? Ok("Refund approved and paid to the buyer") : Fail(resync.message);
                    }
                    return Fail("Approving an undelivered-package refund requires the on-chain refund transaction");
                }
                var hash = req.TxHash!.ToLower();
                if (await _context.TrEscrowTransactions.AnyAsync(x => x.TxHash == hash))
                {
                    return Fail("Transaction hash is already used");
                }
                var verify = await _chain.VerifyEscrowTx(hash, order.OnchainOrderId!, null, EscrowEvents.Refunded);
                if (!verify.ok) return Fail(verify.message);

                refund.Status = "approved";
                refund.DecisionTxHash = hash;
                var sync = await _sync.SyncOrder(order, hash, adminId,
                    "Refund approved by admin (package never arrived), payment returned to buyer" +
                    (adminNote == null ? "" : $": {adminNote}"));
                if (!sync.ok) return Fail(sync.message);

                return Ok("Refund approved and paid to the buyer");
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while deciding refund");
            }
        }
    }
}
