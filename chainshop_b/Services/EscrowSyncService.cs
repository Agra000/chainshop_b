using chainshop_b.Blockchain;
using chainshop_b.Data;
using chainshop_b.Model;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    // Menyamakan status order di database dengan state smart contract escrow.
    // Contract hanya menyimpan status uang (Funded / Shipped / Completed / Refunded / Cancelled);
    // status lain (in_transit, delivered, refund_requested, return_*) dikelola backend dan TIDAK ditimpa di sini.
    public class EscrowSyncService
    {
        private readonly ApplicationDBContext _context;
        private readonly BlockchainService _chain;
        private readonly ILogger<EscrowSyncService> _logger;

        public const string StatusPendingPayment = "pending_payment";
        public const string StatusPaid = "paid";
        public const string StatusProcessing = "processing";
        public const string StatusShipped = "shipped";
        public const string StatusInTransit = "in_transit";
        public const string StatusDelivered = "delivered";
        public const string StatusRefundRequested = "refund_requested";
        public const string StatusReturnPending = "return_pending";
        public const string StatusReturnShipped = "return_shipped";
        public const string StatusReturnDelivered = "return_delivered";
        // v3: buyer sudah menandatangani persetujuan (tanpa gas), menunggu seller klaim dana onchain
        public const string StatusAccepted = "accepted";
        public const string StatusCompleted = "completed";
        public const string StatusRefunded = "refunded";
        public const string StatusCancelled = "cancelled";

        public const string EscrowAddressLabel = "escrow_contract";

        // status yang dananya masih tertahan di escrow dan barangnya sudah dikirim
        public static readonly string[] ShippedStatuses =
        {
            StatusShipped, StatusInTransit, StatusDelivered, StatusRefundRequested,
            StatusReturnPending, StatusReturnShipped, StatusReturnDelivered, StatusAccepted
        };

        public EscrowSyncService(ApplicationDBContext context, BlockchainService chain, ILogger<EscrowSyncService> logger)
        {
            _context = context;
            _chain = chain;
            _logger = logger;
        }

        public static string GenerateTrackingNumber(string? courierName)
        {
            var letters = new string((courierName ?? "CS").Where(char.IsLetter).Take(3).ToArray()).ToUpper();
            if (letters.Length < 2) letters = "CS";
            var random = Random.Shared.Next(100000, 999999);
            return $"{letters}{DateTime.UtcNow:yyMMdd}{random}";
        }

        public void AddTracking(Guid orderId, string status, string? note, string? location, Guid updatedBy)
        {
            _context.TrOrderTrackings.Add(new TrOrderTrackings
            {
                OrderId = orderId,
                Status = status,
                Note = note,
                Location = location,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy
            });
        }

        private void AddEscrowTx(TrOrders order, string txType, double amount, string? txHash, string? from, string? to, Guid updatedBy)
        {
            _context.TrEscrowTransactions.Add(new TrEscrowTransactions
            {
                OrderId = order.Id,
                TxType = txType,
                AmountIdr = amount,
                // tx_hash wajib diisi; jika transisi ditemukan lewat sinkronisasi tanpa tx hash, pakai penanda unik
                TxHash = string.IsNullOrWhiteSpace(txHash) ? $"sync-{order.Id:N}-{txType}" : txHash.ToLower(),
                FromWallet = from,
                ToWallet = to,
                Status = "success",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy
            });
        }

        private async Task RestoreStock(Guid orderId, Guid updatedBy)
        {
            var items = await _context.TrOrderItems.Where(x => x.OrderId == orderId).ToListAsync();
            var productIds = items.Select(x => x.ProductId).ToList();
            var products = await _context.TrProducts.Where(x => productIds.Contains(x.Id)).ToListAsync();

            foreach (var item in items)
            {
                var product = products.FirstOrDefault(x => x.Id == item.ProductId);
                if (product != null)
                {
                    product.Stock += item.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                    product.UpdatedBy = updatedBy;
                }
            }
        }

        private async Task AddSoldCount(Guid orderId, Guid updatedBy)
        {
            var items = await _context.TrOrderItems.Where(x => x.OrderId == orderId).ToListAsync();
            var productIds = items.Select(x => x.ProductId).ToList();
            var products = await _context.TrProducts.Where(x => productIds.Contains(x.Id)).ToListAsync();

            foreach (var item in items)
            {
                var product = products.FirstOrDefault(x => x.Id == item.ProductId);
                if (product != null)
                {
                    product.SoldCount += item.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                    product.UpdatedBy = updatedBy;
                }
            }
        }

        // pengajuan refund terakhir yang masih terbuka (pending / approved)
        public async Task<TrRefundRequests?> LatestOpenRefund(Guid orderId)
        {
            var local = _context.TrRefundRequests.Local
                .Where(x => x.OrderId == orderId && (x.Status == "pending" || x.Status == "approved"))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();
            if (local != null) return local;

            return await _context.TrRefundRequests
                .Where(x => x.OrderId == orderId && (x.Status == "pending" || x.Status == "approved"))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }

        // isi deposit onchain harus sama persis dengan order di database
        public static bool DepositMatches(TrOrders order, OnchainOrderDto onchain)
        {
            return BlockchainService.SameAddress(onchain.Seller, order.Seller?.PayoutWalletAddress) &&
                   onchain.ItemsTotal == BlockchainService.ToUnits(order.ItemsTotal) &&
                   onchain.ShippingFee == BlockchainService.ToUnits(order.ShippingFee) &&
                   onchain.ItemCount == order.ItemCount;
        }

        /// <summary>
        /// Baca state order di smart contract lalu terapkan ke database (status uang, escrow tx, tracking).
        /// <paramref name="txHash"/> = tx yang memicu perubahan (dicatat di riwayat escrow), boleh null.
        /// </summary>
        public async Task<(bool ok, string message)> SyncOrder(TrOrders order, string? txHash, Guid actorId, string? note = null)
        {
            if (!_chain.IsEnabled) return (false, _chain.NotConfiguredMessage);
            if (string.IsNullOrWhiteSpace(order.OnchainOrderId)) return (false, "Order is not linked to the escrow contract");

            var onchain = await _chain.GetOrder(order.OnchainOrderId);
            if (onchain == null || onchain.Status == OnchainStatus.None)
            {
                return (false, "Order is not found in the escrow contract");
            }

            var changed = await Apply(order, onchain, txHash, actorId, note);
            await _context.SaveChangesAsync();
            return (true, changed ? "Order updated from blockchain" : "Order is already up to date");
        }

        public async Task<bool> Apply(TrOrders order, OnchainOrderDto onchain, string? txHash, Guid actorId, string? note)
        {
            // entity bisa basi (mis. diproses cron sementara user sudah memicu aksi yang sama) -> muat ulang
            var entry = _context.Entry(order);
            if (entry.State == EntityState.Unchanged)
            {
                var dbStatus = await _context.TrOrders.AsNoTracking()
                    .Where(x => x.Id == order.Id).Select(x => x.Status).FirstOrDefaultAsync();
                if (dbStatus != null && dbStatus != order.Status) await entry.ReloadAsync();
            }

            order.Seller ??= await _context.MsSellers.FirstOrDefaultAsync(x => x.Id == order.SellerId);
            order.Buyer ??= await _context.MsUsers.FirstOrDefaultAsync(x => x.Id == order.BuyerId);

            // deposit yang tidak cocok dengan order tidak boleh dianggap sebagai pembayaran
            // (hanya untuk transisi Funded -> paid; state akhir seperti Cancelled tetap disinkronkan)
            if (order.Status == StatusPendingPayment && onchain.Status != OnchainStatus.Cancelled && !DepositMatches(order, onchain))
            {
                _logger.LogWarning("Deposit onchain order {Code} tidak cocok dengan database", order.OrderCode);
                return false;
            }

            var before = order.Status;
            var now = DateTime.UtcNow;
            var city = order.Seller?.City;

            order.GasFeeEstimate = BlockchainService.FromUnits(onchain.GasFee);
            order.TotalIdr = BlockchainService.FromUnits(onchain.ItemsTotal + onchain.ShippingFee + onchain.GasFee);

            switch (onchain.Status)
            {
                case OnchainStatus.Funded:
                    if (order.Status == StatusPendingPayment)
                    {
                        order.Status = StatusPaid;
                        order.PaidAt = BlockchainService.FromUnix(onchain.FundedAt) ?? now;
                        AddTracking(order.Id, StatusPaid, note ?? "Payment verified, money is held in escrow", null, actorId);
                    }
                    else if (order.Status == StatusCancelled && order.SellerDeadlineAt == null)
                    {
                        // deposit masuk setelah order dibatalkan -> cron langsung mengembalikan dana (refund operator)
                        AddTracking(order.Id, StatusCancelled,
                            "A payment arrived on-chain after this order was cancelled, it will be refunded automatically",
                            null, actorId);
                    }
                    order.SellerDeadlineAt = BlockchainService.FromUnix(onchain.ShipDeadline);
                    break;

                case OnchainStatus.Shipped:
                    if (order.Status == StatusPaid || order.Status == StatusProcessing)
                    {
                        order.Status = StatusShipped;
                        order.ShippedAt = BlockchainService.FromUnix(onchain.ShippedAt) ?? now;
                        if (!string.IsNullOrWhiteSpace(txHash) && string.IsNullOrWhiteSpace(order.InvoiceTxHash))
                        {
                            order.InvoiceTxHash = txHash.ToLower();
                        }
                        AddTracking(order.Id, StatusShipped,
                            note ?? $"Package handed over to {order.CourierName ?? "courier"}, invoice recorded on-chain", city, actorId);
                    }
                    // status off-chain setelah dikirim (in_transit, delivered, refund, retur) tidak diubah di sini
                    order.SellerDeadlineAt = null;
                    break;

                case OnchainStatus.Cancelled:
                    if (order.Status != StatusCancelled)
                    {
                        await RestoreStock(order.Id, actorId);
                        AddEscrowTx(order, "refund", order.TotalIdr, txHash, EscrowAddressLabel, onchain.Buyer, actorId);
                        order.Status = StatusCancelled;
                        AddTracking(order.Id, StatusCancelled,
                            note ?? "Order cancelled on-chain, the full payment was refunded to buyer", null, actorId);
                    }
                    else if (!await _context.TrEscrowTransactions.AnyAsync(x => x.OrderId == order.Id && x.TxType == "refund"))
                    {
                        // deposit terlambat (order sudah batal di database) sudah dikembalikan onchain
                        AddEscrowTx(order, "refund", order.TotalIdr, txHash, EscrowAddressLabel, onchain.Buyer, actorId);
                        AddTracking(order.Id, StatusCancelled, note ?? "Late payment refunded to buyer on-chain", null, actorId);
                    }
                    order.SellerDeadlineAt = null;
                    break;

                case OnchainStatus.Completed:
                    if (order.Status != StatusCompleted)
                    {
                        var openRefund = await LatestOpenRefund(order.Id);
                        string defaultNote;
                        if (order.Status == StatusAccepted)
                        {
                            defaultNote = "Payment claimed by seller with the buyer's acceptance signature";
                        }
                        else if (order.Status == StatusReturnPending || openRefund?.Status == "approved")
                        {
                            defaultNote = "Return was not shipped in time, refund cancelled and money released to seller";
                            await MarkRefund(order.Id, "expired", txHash, actorId);
                        }
                        else if (order.Status == StatusRefundRequested || openRefund?.Status == "pending")
                        {
                            defaultNote = "No decision was made in time, money released to seller";
                            await MarkRefund(order.Id, "expired", txHash, actorId);
                        }
                        else if (order.BuyerDeadlineAt != null && order.BuyerDeadlineAt < now)
                        {
                            defaultNote = "No response from buyer in time, order accepted automatically and money released to seller";
                        }
                        else
                        {
                            defaultNote = "Order accepted by buyer, money released to seller";
                        }

                        AddEscrowTx(order, "release", order.TotalIdr, txHash, EscrowAddressLabel, onchain.Seller, actorId);
                        await AddSoldCount(order.Id, actorId);
                        order.Status = StatusCompleted;
                        order.DeliveredAt ??= now;
                        order.CompletedAt = now;
                        AddTracking(order.Id, StatusCompleted, note ?? defaultNote, null, actorId);
                    }
                    ClearDeadlines(order);
                    break;

                case OnchainStatus.Refunded:
                    if (order.Status != StatusRefunded)
                    {
                        var toBuyer = BlockchainService.FromUnits(onchain.ItemsTotal + onchain.ShippingFee);
                        var itemReturned = order.Status == StatusReturnShipped || order.Status == StatusReturnDelivered;

                        if (itemReturned) await RestoreStock(order.Id, actorId);
                        await MarkRefund(order.Id, "refunded", txHash, actorId);

                        AddEscrowTx(order, "refund", toBuyer, txHash, EscrowAddressLabel, onchain.Buyer, actorId);
                        order.Status = StatusRefunded;
                        order.CompletedAt = now;
                        AddTracking(order.Id, StatusRefunded,
                            note ?? $"Refund completed: {toBuyer:0.######} mUSDT returned to buyer, gas estimate paid to seller", null, actorId);
                    }
                    ClearDeadlines(order);
                    break;
            }

            order.UpdatedAt = now;
            order.UpdatedBy = actorId;
            return before != order.Status;
        }

        private static void ClearDeadlines(TrOrders order)
        {
            order.SellerDeadlineAt = null;
            order.BuyerDeadlineAt = null;
            order.ReturnDeadlineAt = null;
            order.BuyerRemainingSeconds = null;
        }

        // update status pengajuan refund terakhir yang masih terbuka
        public async Task MarkRefund(Guid orderId, string status, string? txHash, Guid actorId)
        {
            var refund = await LatestOpenRefund(orderId);
            if (refund == null) return;

            refund.Status = status;
            refund.UpdatedAt = DateTime.UtcNow;
            refund.UpdatedBy = actorId;
            if (!string.IsNullOrWhiteSpace(txHash)) refund.DecisionTxHash ??= txHash.ToLower();
        }

        // batalkan order yang belum dibayar (tidak ada deposit onchain) dan kembalikan stok
        public async Task CancelUnpaid(TrOrders order, Guid actorId, string note)
        {
            await RestoreStock(order.Id, actorId);
            order.Status = StatusCancelled;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = actorId;
            AddTracking(order.Id, StatusCancelled, note, null, actorId);
            await _context.SaveChangesAsync();
        }
    }
}
