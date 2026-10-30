using chainshop_b.Blockchain;
using chainshop_b.Data;
using chainshop_b.Model;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    // Menjalankan aksi otomatis untuk SATU order berdasarkan timer off-chain di database.
    // Dipakai cron (EscrowAutomationService) dan endpoint /Order/run-auto (tombol "... now" di frontend).
    // Transaksi dikirim wallet operator (OPERATOR_ROLE): release / refund.
    public class EscrowAutomationRunner
    {
        private readonly ApplicationDBContext _context;
        private readonly BlockchainService _chain;
        private readonly EscrowSyncService _sync;
        private readonly ILogger<EscrowAutomationRunner> _logger;

        public enum AutoAction
        {
            None,
            CancelNotShipped, //    seller tidak kirim sebelum batas -> refund penuh ke buyer
            RefundLateDeposit, //   deposit masuk setelah order batal -> refund penuh ke buyer
            ReleaseBuyerSilent, //  buyer diam setelah barang sampai -> dana ke seller
            ReleaseReturnExpired, // buyer tidak kirim retur -> dana ke seller
            RefundReturnUnconfirmed // seller tidak konfirmasi retur -> refund ke buyer
        }

        private static readonly Dictionary<AutoAction, string> Notes = new()
        {
            [AutoAction.CancelNotShipped] = "Seller did not ship before the deadline, order cancelled automatically and the full payment refunded to buyer",
            [AutoAction.RefundLateDeposit] = "Late payment refunded to buyer automatically",
            [AutoAction.ReleaseBuyerSilent] = "No response from buyer in time, order accepted automatically and money released to seller",
            [AutoAction.ReleaseReturnExpired] = "Return was not shipped in time, refund cancelled and money released to seller",
            [AutoAction.RefundReturnUnconfirmed] = "Seller did not confirm the returned item in time, refund sent to buyer automatically"
        };

        public EscrowAutomationRunner(ApplicationDBContext context, BlockchainService chain, EscrowSyncService sync, ILogger<EscrowAutomationRunner> logger)
        {
            _context = context;
            _chain = chain;
            _sync = sync;
            _logger = logger;
        }

        // aksi yang sudah jatuh tempo menurut database (belum melihat onchain)
        public static AutoAction DueAction(TrOrders order, DateTime now)
        {
            switch (order.Status)
            {
                case EscrowSyncService.StatusPaid:
                case EscrowSyncService.StatusProcessing:
                    return order.SellerDeadlineAt != null && order.SellerDeadlineAt < now ? AutoAction.CancelNotShipped : AutoAction.None;
                case EscrowSyncService.StatusCancelled:
                    return order.SellerDeadlineAt != null ? AutoAction.RefundLateDeposit : AutoAction.None;
                case EscrowSyncService.StatusDelivered:
                    return order.BuyerDeadlineAt != null && order.BuyerDeadlineAt < now ? AutoAction.ReleaseBuyerSilent : AutoAction.None;
                case EscrowSyncService.StatusReturnPending:
                    return order.ReturnDeadlineAt != null && order.ReturnDeadlineAt < now ? AutoAction.ReleaseReturnExpired : AutoAction.None;
                case EscrowSyncService.StatusReturnDelivered:
                    return order.ReturnDeadlineAt != null && order.ReturnDeadlineAt < now ? AutoAction.RefundReturnUnconfirmed : AutoAction.None;
                default:
                    return AutoAction.None;
            }
        }

        /// <summary>
        /// Jalankan aksi otomatis untuk order ini jika sudah jatuh tempo.
        /// Selalu cek state onchain dulu: jika dana sudah dicairkan pihak lain, cukup sinkronkan database.
        /// </summary>
        public async Task<(bool ok, string message)> Run(Guid orderId)
        {
            if (!_chain.IsEnabled) return (false, _chain.NotConfiguredMessage);

            var order = await _context.TrOrders
                .Include(x => x.Seller)
                .Include(x => x.Buyer)
                .FirstOrDefaultAsync(x => x.Id == orderId);
            if (order == null || string.IsNullOrWhiteSpace(order.OnchainOrderId)) return (false, "Order not found");

            var onchain = await _chain.GetOrder(order.OnchainOrderId);
            if (onchain == null || onchain.Status == OnchainStatus.None) return (false, "Order is not found in the escrow contract");

            // SELALU sinkronkan dulu: mis. markShipped sudah masuk onchain tapi /Order/ship gagal tersimpan,
            // order harus jadi "shipped" dulu supaya cron tidak salah me-refund barang yang sudah dikirim
            await _sync.Apply(order, onchain, null, Guid.Empty, null);
            await _context.SaveChangesAsync();

            var action = DueAction(order, DateTime.UtcNow);
            // tiap aksi hanya boleh jalan pada state onchain yang tepat
            var requiredState = action switch
            {
                AutoAction.CancelNotShipped or AutoAction.RefundLateDeposit => OnchainStatus.Funded,
                AutoAction.None => OnchainStatus.None,
                _ => OnchainStatus.Shipped
            };
            var isRelease = action is AutoAction.ReleaseBuyerSilent or AutoAction.ReleaseReturnExpired;
            var canAct = action != AutoAction.None && onchain.Status == requiredState;

            if (!canAct)
            {
                return (true, action == AutoAction.None ? "Nothing is due for this order yet" : "Order is already up to date");
            }

            if (!_chain.CanSendAutoTx)
            {
                return (false, "Blockchain:OperatorPrivateKey is not configured on the backend");
            }

            Nethereum.RPC.Eth.DTOs.TransactionReceipt receipt;
            try
            {
                receipt = isRelease ? await _chain.SendRelease(order.OnchainOrderId) : await _chain.SendRefund(order.OnchainOrderId);
            }
            catch (Exception ex)
            {
                // mis. pihak lain lebih dulu mencairkan dana (revert InvalidStatus) -> sinkronkan saja
                _logger.LogWarning(ex, "Auto {Action} order {Code} gagal dikirim", action, order.OrderCode);
                var resynced = await _sync.SyncOrder(order, null, Guid.Empty);
                return (false, resynced.ok ? "The automatic transaction was not sent, order re-synced from blockchain" : "The automatic transaction could not be sent");
            }
            if (receipt.Status?.Value != 1)
            {
                _logger.LogWarning("Auto {Action} order {Code} gagal (tx {Tx})", action, order.OrderCode, receipt.TransactionHash);
                return (false, "The automatic transaction failed on-chain");
            }

            _logger.LogInformation("Auto {Action} order {Code} tx {Tx}", action, order.OrderCode, receipt.TransactionHash);
            var sync = await _sync.SyncOrder(order, receipt.TransactionHash, Guid.Empty, Notes.GetValueOrDefault(action));
            return (sync.ok, sync.ok ? "Done" : sync.message);
        }

        // order belum dibayar terlalu lama -> batalkan supaya stok kembali (kecuali deposit ternyata sudah onchain)
        public async Task ExpireUnpaid(Guid orderId)
        {
            var order = await _context.TrOrders.FirstOrDefaultAsync(x => x.Id == orderId);
            if (order == null || order.Status != EscrowSyncService.StatusPendingPayment) return;

            if (!string.IsNullOrWhiteSpace(order.OnchainOrderId) && _chain.IsEnabled)
            {
                var onchain = await _chain.GetOrder(order.OnchainOrderId);
                if (onchain != null && onchain.Status != OnchainStatus.None)
                {
                    await _sync.SyncOrder(order, null, Guid.Empty);
                    if (order.Status != EscrowSyncService.StatusPendingPayment) return;

                    // deposit onchain tidak cocok dengan order -> batalkan; cron lalu me-refund deposit itu ke buyer
                    if (onchain.Status == OnchainStatus.Funded)
                    {
                        order.SellerDeadlineAt = BlockchainService.FromUnix(onchain.ShipDeadline) ?? DateTime.UtcNow;
                        await _sync.CancelUnpaid(order, Guid.Empty,
                            "Order cancelled: the on-chain payment does not match this order, it will be refunded automatically");
                    }
                    return;
                }
            }

            await _sync.CancelUnpaid(order, Guid.Empty, "Order cancelled automatically: not paid within 24 hours");
        }
    }
}
