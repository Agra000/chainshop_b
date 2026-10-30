using chainshop_b.Blockchain;
using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Numerics;
using System.Text.Json;

namespace chainshop_b.Services
{
    public class OrderService
    {
        private readonly ApplicationDBContext _context;
        private readonly BlockchainService _chain;
        private readonly EscrowSyncService _sync;

        // alur status order (onchain = transaksi smart contract, sisanya hanya di database):
        // pending_payment -> paid (deposit, onchain) -> processing (seller acc)
        //   -> shipped (markShipped + invoice, onchain) -> in_transit (mock) -> delivered (seller tandai sampai)
        //   -> completed (release, onchain: buyer terima / cron saat timer habis)
        // refund (off-chain sampai keputusan akhir): delivered -> refund_requested -> return_pending -> return_shipped
        //   -> return_delivered -> refunded (refund onchain oleh seller / cron)
        // cancel: pending_payment -> cancelled (buyer), paid/processing -> cancelled (refund onchain: seller tolak / cron)
        private const string StatusPendingPayment = EscrowSyncService.StatusPendingPayment;
        private const string StatusPaid = EscrowSyncService.StatusPaid;
        private const string StatusProcessing = EscrowSyncService.StatusProcessing;
        private const string StatusShipped = EscrowSyncService.StatusShipped;
        private const string StatusInTransit = EscrowSyncService.StatusInTransit;
        private const string StatusDelivered = EscrowSyncService.StatusDelivered;
        private const string StatusCancelled = EscrowSyncService.StatusCancelled;

        private const string EscrowAddress = EscrowSyncService.EscrowAddressLabel;
        private const int MaxItemNameLength = 100;

        // semua event escrow, untuk verifikasi tx generik (sync)
        private static readonly string[] AllEscrowEvents =
        {
            EscrowEvents.Deposited, EscrowEvents.OrderCancelled, EscrowEvents.Shipped,
            EscrowEvents.Released, EscrowEvents.Refunded
        };

        public OrderService(ApplicationDBContext context, BlockchainService chain, EscrowSyncService sync)
        {
            _context = context;
            _chain = chain;
            _sync = sync;
        }

        private static string GenerateOrderCode()
        {
            return $"CS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        }

        private static string GenerateInvoiceId()
        {
            return $"INV-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }

        private static string FormatDate(DateTime? date)
        {
            return date == null ? "" : date.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string InvoiceItemName(string name)
        {
            var trimmed = (name ?? "").Trim();
            return trimmed.Length <= MaxItemNameLength ? trimmed : trimmed[..MaxItemNameLength];
        }

        private static ResultMessageResponse Fail(string message) => new() { Status = false, Message = message };

        private async Task<bool> IsTxHashUsed(string txHash)
        {
            var hash = txHash.ToLower();
            return await _context.TrEscrowTransactions.AnyAsync(x => x.TxHash == hash) ||
                   await _context.TrOrders.AnyAsync(x => x.InvoiceTxHash == hash) ||
                   await _context.TrRefundRequests.AnyAsync(x => x.RequestTxHash == hash || x.DecisionTxHash == hash);
        }

        // buyer, seller pemilik order, atau admin
        public async Task<bool> CanAccessOrder(Guid loginUserId, Guid orderId)
        {
            var order = await _context.TrOrders.Include(x => x.Seller).AsNoTracking().FirstOrDefaultAsync(x => x.Id == orderId);
            if (order == null) return false;
            if (order.BuyerId == loginUserId || order.Seller?.UserId == loginUserId) return true;
            return await IsAdmin(loginUserId);
        }

        private async Task<bool> IsAdmin(Guid userId)
        {
            return await _context.MsUsers.AnyAsync(x => x.Id == userId && x.Role == "admin");
        }

        // ===== Checkout =====

        private class SellerGroup
        {
            public MsSellers Seller = null!;
            public List<TrCartItems> Items = new();
            public BigInteger ItemsTotalUnits;
            public BigInteger ShippingUnits;
            public BigInteger GasUnits;
        }

        // validasi cart + hitung rincian per seller (harga barang, ongkir, estimasi gas dari smart contract)
        private async Task<(string? error, List<SellerGroup> groups)> BuildSellerGroups(Guid userId)
        {
            var groups = new List<SellerGroup>();

            var cartItems = await _context.TrCartItems
                .Include(x => x.Product)
                .ThenInclude(x => x!.Seller)
                .Where(x => x.UserId == userId && x.IsSelected && x.Quantity > 0)
                .ToListAsync();

            if (!cartItems.Any()) return ("No item selected in cart", groups);

            foreach (var item in cartItems)
            {
                if (item.Product == null || item.Product.Status != "active") return ("Some items are no longer available", groups);
                if (item.Product.Stock < item.Quantity) return ($"Insufficient stock for {item.Product.Name}", groups);
                if (item.Product.Seller == null) return ("Some items are no longer available", groups);
                if (item.Product.Seller.UserId == userId) return ("You cannot buy your own product", groups);
            }

            var gasCache = new Dictionary<int, BigInteger>();
            var shippingUnits = BlockchainService.ToUnits(_chain.ShippingFee);

            // 1 order per seller, karena escrow di-release ke wallet masing-masing seller
            foreach (var group in cartItems.GroupBy(x => x.Product!.SellerId))
            {
                var items = group.ToList();
                var itemCount = items.Count;
                if (itemCount > 50) return ("Maximum 50 different items per seller in one checkout", groups);

                if (!gasCache.TryGetValue(itemCount, out var gasUnits))
                {
                    gasUnits = await _chain.QuoteGasFeeUnits(itemCount);
                    gasCache[itemCount] = gasUnits;
                }

                groups.Add(new SellerGroup
                {
                    Seller = items.First().Product!.Seller!,
                    Items = items,
                    ItemsTotalUnits = items.Aggregate(BigInteger.Zero,
                        (sum, x) => sum + BlockchainService.ToUnits(x.Product!.PriceIdr) * x.Quantity),
                    ShippingUnits = shippingUnits,
                    GasUnits = gasUnits
                });
            }

            return (null, groups);
        }

        public async Task<CheckoutPreviewResponse> GetCheckoutPreview(Guid userId)
        {
            try
            {
                if (!_chain.IsEnabled) return new CheckoutPreviewResponse { Status = false, Message = _chain.NotConfiguredMessage };

                var (error, groups) = await BuildSellerGroups(userId);
                if (error != null) return new CheckoutPreviewResponse { Status = false, Message = error };

                var res = new CheckoutPreviewResponse { Status = true, Message = "Success", escrowAddress = _chain.EscrowAddress };
                foreach (var g in groups)
                {
                    var total = g.ItemsTotalUnits + g.ShippingUnits + g.GasUnits;
                    res.groups.Add(new CheckoutPreviewGroup
                    {
                        sellerId = g.Seller.Id,
                        shopName = g.Seller.ShopName,
                        sellerWallet = g.Seller.PayoutWalletAddress,
                        itemCount = g.Items.Count,
                        itemsTotal = BlockchainService.FromUnits(g.ItemsTotalUnits),
                        shippingFee = BlockchainService.FromUnits(g.ShippingUnits),
                        gasFeeEstimate = BlockchainService.FromUnits(g.GasUnits),
                        total = BlockchainService.FromUnits(total),
                        items = g.Items.Select(x => new CheckoutPreviewItem
                        {
                            productId = x.ProductId,
                            name = x.Product!.Name,
                            image = x.Product.Image,
                            price = x.Product.PriceIdr,
                            qty = x.Quantity,
                            lineTotal = BlockchainService.FromUnits(BlockchainService.ToUnits(x.Product.PriceIdr) * x.Quantity)
                        }).ToList()
                    });
                }

                res.grandTotal = res.groups.Sum(x => x.total);
                res.totalGasFee = res.groups.Sum(x => x.gasFeeEstimate);
                return res;
            }
            catch (Exception ex)
            {
                return new CheckoutPreviewResponse { Status = false, Message = "Server error occurred while calculating checkout" };
            }
        }

        public async Task<CheckoutResponse> Checkout(Guid userId, CheckoutRequest req)
        {
            if (!_chain.IsEnabled) return new CheckoutResponse { Status = false, Message = _chain.NotConfiguredMessage };

            await using var trx = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.MsUsers.FirstOrDefaultAsync(x => x.Id == userId);
                if (user == null) return new CheckoutResponse { Status = false, Message = "User not found" };

                var address = await _context.MsAddresses.FirstOrDefaultAsync(x => x.Id == req.AddressId && x.UserId == userId);
                if (address == null) return new CheckoutResponse { Status = false, Message = "Shipping address not found" };

                var (error, groups) = await BuildSellerGroups(userId);
                if (error != null) return new CheckoutResponse { Status = false, Message = error };

                var result = new CheckoutResponse
                {
                    Status = true,
                    Message = "Order created, please continue the payment"
                };

                foreach (var g in groups)
                {
                    var order = new TrOrders
                    {
                        OrderCode = GenerateOrderCode(),
                        BuyerId = userId,
                        SellerId = g.Seller.Id,
                        AddressId = address.Id,
                        ItemsTotal = BlockchainService.FromUnits(g.ItemsTotalUnits),
                        ShippingFee = BlockchainService.FromUnits(g.ShippingUnits),
                        GasFeeEstimate = BlockchainService.FromUnits(g.GasUnits),
                        TotalIdr = BlockchainService.FromUnits(g.ItemsTotalUnits + g.ShippingUnits + g.GasUnits),
                        ItemCount = g.Items.Count,
                        Status = StatusPendingPayment,
                        PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "crypto" : req.PaymentMethod,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = userId
                    };
                    order.OnchainOrderId = BlockchainService.ToOnchainOrderId(order.Id);
                    _context.TrOrders.Add(order);

                    foreach (var item in g.Items)
                    {
                        _context.TrOrderItems.Add(new TrOrderItems
                        {
                            OrderId = order.Id,
                            ProductId = item.ProductId,
                            ProductNameSnapshot = item.Product!.Name,
                            ProductPriceSnapshot = item.Product.PriceIdr,
                            Quantity = item.Quantity,
                            LineTotalIdr = BlockchainService.FromUnits(BlockchainService.ToUnits(item.Product.PriceIdr) * item.Quantity),
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = userId
                        });

                        // stock di-reserve saat checkout, dikembalikan jika order dibatalkan
                        item.Product.Stock -= item.Quantity;
                        item.Product.UpdatedAt = DateTime.UtcNow;
                        item.Product.UpdatedBy = userId;
                    }

                    _sync.AddTracking(order.Id, StatusPendingPayment, "Waiting for payment", null, userId);

                    result.orders.Add(new CheckoutOrderResponse
                    {
                        orderId = order.Id,
                        orderCode = order.OrderCode,
                        sellerId = g.Seller.Id,
                        shopName = g.Seller.ShopName,
                        sellerWallet = g.Seller.PayoutWalletAddress,
                        totalIdr = order.TotalIdr,
                        itemsTotal = order.ItemsTotal,
                        shippingFee = order.ShippingFee,
                        gasFeeEstimate = order.GasFeeEstimate,
                        itemCount = order.ItemCount,
                        onchainOrderId = order.OnchainOrderId,
                        itemsTotalUnits = g.ItemsTotalUnits.ToString(),
                        shippingFeeUnits = g.ShippingUnits.ToString(),
                        gasFeeUnits = g.GasUnits.ToString()
                    });
                }

                _context.TrCartItems.RemoveRange(groups.SelectMany(x => x.Items));

                await _context.SaveChangesAsync();
                await trx.CommitAsync();

                result.grandTotal = result.orders.Sum(x => x.totalIdr);
                return result;
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                return new CheckoutResponse
                {
                    Status = false,
                    Message = $"Server error occurred while creating order"
                };
            }
        }

        // Payment Verification: buyer sudah deposit ke smart contract escrow
        public async Task<ResultMessageResponse> PayOrder(Guid userId, Guid orderId, EscrowTxRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Buyer)
                    .Include(x => x.Seller)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);

                if (order == null) return Fail("Order not found");
                if (order.Status != StatusPendingPayment) return Fail("Order is already paid or cancelled");
                if (string.IsNullOrWhiteSpace(order.OnchainOrderId)) return Fail("Order is not linked to the escrow contract");
                if (!BlockchainService.IsValidTxHash(req.TxHash)) return Fail("Invalid transaction hash");
                if (await IsTxHashUsed(req.TxHash)) return Fail("Transaction hash is already used");

                var verify = await _chain.VerifyEscrowTx(req.TxHash, order.OnchainOrderId, null, EscrowEvents.Deposited);
                if (!verify.ok) return Fail(verify.message);

                var onchain = await _chain.GetOrder(order.OnchainOrderId);
                if (onchain == null || onchain.Status == OnchainStatus.None) return Fail("Deposit is not found in the escrow contract");

                // pastikan isi deposit sama persis dengan order di database
                if (!EscrowSyncService.DepositMatches(order, onchain)) return Fail("Deposit on-chain does not match this order");

                if (!string.IsNullOrWhiteSpace(order.Buyer?.WalletAddress) &&
                    !BlockchainService.SameAddress(onchain.Buyer, order.Buyer.WalletAddress))
                {
                    return Fail("Please pay with the wallet you logged in with");
                }

                _context.TrEscrowTransactions.Add(new TrEscrowTransactions
                {
                    OrderId = order.Id,
                    TxType = "hold",
                    AmountIdr = BlockchainService.FromUnits(onchain.ItemsTotal + onchain.ShippingFee + onchain.GasFee),
                    TxHash = req.TxHash.ToLower(),
                    FromWallet = onchain.Buyer,
                    ToWallet = EscrowAddress,
                    Status = "success",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId
                });

                // Make an order -> order masuk ke order list seller (status dari state onchain)
                await _sync.Apply(order, onchain, req.TxHash, userId, "Payment verified on-chain, money is held in escrow");
                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Payment verified, your money is safely held in escrow"
                };
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while verifying payment");
            }
        }

        public async Task<ResultMessageResponse> CancelOrder(Guid userId, Guid orderId)
        {
            try
            {
                var order = await _context.TrOrders.FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);
                if (order == null) return Fail("Order not found");
                if (order.Status != StatusPendingPayment) return Fail("Only unpaid order can be cancelled");

                // jika ternyata deposit sudah masuk onchain (mis. backend gagal menerima tx hash), jangan batalkan
                if (_chain.IsEnabled && !string.IsNullOrWhiteSpace(order.OnchainOrderId))
                {
                    var onchain = await _chain.GetOrder(order.OnchainOrderId);
                    if (onchain != null && onchain.Status != OnchainStatus.None)
                    {
                        await _sync.Apply(order, onchain, null, userId, "Payment found on-chain, money is held in escrow");
                        await _context.SaveChangesAsync();
                        return Fail("This order is already paid on-chain and cannot be cancelled");
                    }
                }

                var items = await _context.TrOrderItems.Where(x => x.OrderId == order.Id).ToListAsync();
                var productIds = items.Select(x => x.ProductId).ToList();
                var products = await _context.TrProducts.Where(x => productIds.Contains(x.Id)).ToListAsync();
                foreach (var item in items)
                {
                    var product = products.FirstOrDefault(x => x.Id == item.ProductId);
                    if (product != null)
                    {
                        product.Stock += item.Quantity;
                        product.UpdatedAt = DateTime.UtcNow;
                        product.UpdatedBy = userId;
                    }
                }

                order.Status = StatusCancelled;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = userId;

                _sync.AddTracking(order.Id, StatusCancelled, "Order cancelled by buyer", null, userId);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Order cancelled successfully"
                };
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while cancelling order");
            }
        }

        private IQueryable<GetOrderResponse> ProjectOrders(IQueryable<TrOrders> query)
        {
            return query
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new GetOrderResponse
                {
                    Id = x.Id,
                    orderCode = x.OrderCode,
                    status = x.Status,
                    totalIdr = x.TotalIdr,
                    paymentMethod = x.PaymentMethod,
                    buyerName = x.Buyer == null ? "" : x.Buyer.Name,
                    shopName = x.Seller == null ? "" : x.Seller.ShopName,
                    itemCount = _context.TrOrderItems.Where(i => i.OrderId == x.Id).Sum(i => i.Quantity),
                    firstItemImage = _context.TrOrderItems.Where(i => i.OrderId == x.Id && i.Product != null).Select(i => i.Product!.Image).FirstOrDefault(),
                    trackingNumber = x.TrackingNumber,
                    createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    refundCount = x.RefundCount
                });
        }

        public async Task<List<GetOrderResponse>> GetBuyerOrders(Guid userId, string? status)
        {
            try
            {
                var query = _context.TrOrders.Where(x => x.BuyerId == userId);
                if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
                return await ProjectOrders(query).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetOrderResponse>();
            }
        }

        // Look order list (seller)
        public async Task<List<GetOrderResponse>> GetSellerOrders(Guid sellerId, string? status)
        {
            try
            {
                // order yang belum dibayar tidak ditampilkan ke seller
                var query = _context.TrOrders.Where(x => x.SellerId == sellerId && x.Status != StatusPendingPayment);
                if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
                return await ProjectOrders(query).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetOrderResponse>();
            }
        }

        public static GetRefundResponse MapRefund(TrRefundRequests x)
        {
            return new GetRefundResponse
            {
                Id = x.Id,
                orderId = x.OrderId,
                orderCode = x.Order?.OrderCode,
                orderStatus = x.Order?.Status,
                buyerName = x.Order?.Buyer?.Name,
                buyerWallet = x.Order?.Buyer?.WalletAddress,
                shopName = x.Order?.Seller?.ShopName,
                sellerWallet = x.Order?.Seller?.PayoutWalletAddress,
                totalPaid = x.Order?.TotalIdr ?? 0,
                refundAmount = x.Order == null ? 0 : x.Order.ItemsTotal + x.Order.ShippingFee,
                attempt = x.Attempt,
                reason = x.Reason,
                photoUrl = x.PhotoUrl,
                invoiceTxHash = x.InvoiceTxHash,
                requestTxHash = x.RequestTxHash,
                notDelivered = x.NotDelivered,
                status = x.Status,
                adminNote = x.AdminNote,
                decisionTxHash = x.DecisionTxHash,
                decidedAt = FormatDate(x.DecidedAt),
                createdAt = FormatDate(x.CreatedAt)
            };
        }

        // detail order + tracking delivery, bisa diakses buyer (userId), seller (sellerId), atau admin (adminView)
        public async Task<GetOrderDetailResponse> GetOrderDetail(Guid orderId, Guid? userId, Guid? sellerId, bool adminView = false)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Buyer)
                    .Include(x => x.Seller)
                    .Include(x => x.Address)
                    .Include(x => x.Driver)
                    .ThenInclude(x => x!.CourierCompany)
                    .FirstOrDefaultAsync(x => x.Id == orderId);

                if (order == null || (!adminView && order.BuyerId != userId && order.SellerId != sellerId))
                {
                    return new GetOrderDetailResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                var reviewedItems = await _context.TrProductReviews
                    .Where(x => x.OrderItem != null && x.OrderItem.OrderId == order.Id)
                    .Select(x => x.OrderItemId)
                    .ToListAsync();

                var items = await _context.TrOrderItems
                    .Where(x => x.OrderId == order.Id)
                    .Select(x => new GetOrderItemResponse
                    {
                        Id = x.Id,
                        productId = x.ProductId,
                        productName = x.ProductNameSnapshot,
                        image = x.Product == null ? "" : x.Product.Image,
                        price = x.ProductPriceSnapshot,
                        qty = x.Quantity,
                        lineTotal = x.LineTotalIdr
                    }).ToListAsync();

                items.ForEach(x => x.isReviewed = reviewedItems.Contains(x.Id));

                var trackings = await _context.TrOrderTrackings
                    .Where(x => x.OrderId == order.Id)
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => new GetOrderTrackingResponse
                    {
                        status = x.Status,
                        note = x.Note,
                        location = x.Location,
                        createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToListAsync();

                var escrows = await _context.TrEscrowTransactions
                    .Where(x => x.OrderId == order.Id)
                    .OrderBy(x => x.CreatedAt)
                    .Select(x => new GetEscrowTxResponse
                    {
                        txType = x.TxType,
                        amountIdr = x.AmountIdr,
                        txHash = x.TxHash,
                        fromWallet = x.FromWallet,
                        toWallet = x.ToWallet,
                        status = x.Status,
                        createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToListAsync();

                var refunds = (await _context.TrRefundRequests
                        .Where(x => x.OrderId == order.Id)
                        .OrderBy(x => x.CreatedAt)
                        .ToListAsync())
                    .Select(MapRefund)
                    .ToList();

                string? notDeliveredRefundAt = null;
                if (order.ShippedAt != null)
                {
                    notDeliveredRefundAt = FormatDate(order.ShippedAt.Value.Add(_chain.NotDeliveredWindow));
                }

                return new GetOrderDetailResponse
                {
                    Status = true,
                    Message = "Success",
                    orderId = order.Id,
                    orderCode = order.OrderCode,
                    orderStatus = order.Status,
                    totalIdr = order.TotalIdr,
                    paymentMethod = order.PaymentMethod,
                    buyerName = order.Buyer?.Name,
                    buyerWallet = order.Buyer?.WalletAddress,
                    sellerId = order.SellerId,
                    shopName = order.Seller?.ShopName,
                    sellerWallet = order.Seller?.PayoutWalletAddress,
                    address = order.Address == null ? null : new GetAddressResponse
                    {
                        Id = order.Address.Id,
                        Label = order.Address.Label,
                        RecipientName = order.Address.RecipientName,
                        Phone = order.Address.Phone,
                        FullAddress = order.Address.FullAddress,
                        City = order.Address.City,
                        Province = order.Address.Province,
                        PostalCode = order.Address.PostalCode,
                        IsDefault = order.Address.IsDefault
                    },
                    trackingNumber = order.TrackingNumber,
                    invoiceHash = order.InvoiceHash,
                    invoiceTxHash = order.InvoiceTxHash,
                    driver = order.Driver == null ? null : new GetDriverResponse
                    {
                        Id = order.Driver.Id,
                        fullName = order.Driver.FullName,
                        phone = order.Driver.Phone,
                        vehicleType = order.Driver.VehicleType,
                        plateNumber = order.Driver.PlateNumber,
                        courierCompanyId = order.Driver.CourierCompanyId,
                        courierCompany = order.Driver.CourierCompany?.Name
                    },
                    createdAt = FormatDate(order.CreatedAt),
                    paidAt = FormatDate(order.PaidAt),
                    shippedAt = FormatDate(order.ShippedAt),
                    deliveredAt = FormatDate(order.DeliveredAt),
                    completedAt = FormatDate(order.CompletedAt),
                    items = items,
                    trackings = trackings,
                    escrowTransactions = escrows,
                    onchainOrderId = order.OnchainOrderId,
                    escrowAddress = _chain.EscrowAddress,
                    explorerUrl = _chain.ExplorerUrl,
                    invoiceId = order.InvoiceId,
                    courierName = order.CourierName ?? order.Driver?.CourierCompany?.Name,
                    itemsTotal = order.ItemsTotal,
                    shippingFee = order.ShippingFee,
                    gasFeeEstimate = order.GasFeeEstimate,
                    itemCount = order.ItemCount,
                    sellerDeadlineAt = FormatDate(order.SellerDeadlineAt),
                    buyerDeadlineAt = FormatDate(order.BuyerDeadlineAt),
                    returnDeadlineAt = FormatDate(order.ReturnDeadlineAt),
                    notDeliveredRefundAt = notDeliveredRefundAt,
                    refundCount = order.RefundCount,
                    maxRefundRequests = _chain.MaxRefundRequests,
                    statusBeforeRefund = order.StatusBeforeRefund,
                    returnTrackingNumber = order.ReturnTrackingNumber,
                    buyerAcceptSignature = order.BuyerAcceptSignature,
                    acceptedAt = FormatDate(order.AcceptedAt),
                    refunds = refunds
                };
            }
            catch (Exception ex)
            {
                return new GetOrderDetailResponse
                {
                    Status = false,
                    Message = $"Server error occurred while getting order detail"
                };
            }
        }

        // seller menerima order (acc, offchain) dan mulai memproses barang
        public async Task<ResultMessageResponse> ProcessOrder(Guid sellerId, Guid orderId)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);

                if (order == null) return Fail("Order not found");
                if (order.Status != StatusPaid) return Fail("Only paid order can be processed");
                if (order.SellerDeadlineAt != null && order.SellerDeadlineAt < DateTime.UtcNow)
                {
                    return Fail("The 3-day deadline has passed, this order will be cancelled and refunded automatically");
                }

                var updatedBy = order.Seller!.UserId;
                order.Status = StatusProcessing;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = updatedBy;

                _sync.AddTracking(order.Id, StatusProcessing, "Seller accepted the order and is packing it", order.Seller.City, updatedBy);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Order is now being processed"
                };
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while processing order");
            }
        }

        // seller menolak order: refund onchain saat Funded -> semua dana kembali ke buyer
        public async Task<ResultMessageResponse> RejectOrder(Guid sellerId, Guid orderId, EscrowTxRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .Include(x => x.Buyer)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);

                if (order == null) return Fail("Order not found");
                if (order.Status != StatusPaid && order.Status != StatusProcessing) return Fail("Order can no longer be rejected");
                if (!BlockchainService.IsValidTxHash(req.TxHash)) return Fail("Invalid refund transaction hash");
                if (await IsTxHashUsed(req.TxHash)) return Fail("Transaction hash is already used");

                var verify = await _chain.VerifyEscrowTx(req.TxHash, order.OnchainOrderId!, order.Seller!.PayoutWalletAddress, EscrowEvents.OrderCancelled);
                if (!verify.ok) return Fail(verify.message);

                var note = string.IsNullOrWhiteSpace(req.Note)
                    ? "Order rejected by seller, the full payment was refunded to buyer"
                    : $"Order rejected by seller: {req.Note.Trim()} (full payment refunded to buyer)";
                var sync = await _sync.SyncOrder(order, req.TxHash, order.Seller.UserId, note);
                if (!sync.ok) return Fail(sync.message);

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Order rejected and money refunded to buyer"
                };
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while rejecting order");
            }
        }

        private async Task<List<ShipmentInvoiceItem>> BuildInvoiceItems(Guid orderId)
        {
            var items = await _context.TrOrderItems
                .Where(x => x.OrderId == orderId)
                .OrderBy(x => x.ProductNameSnapshot)
                .ThenBy(x => x.ProductId)
                .ToListAsync();

            return items.Select(x => new ShipmentInvoiceItem
            {
                name = InvoiceItemName(x.ProductNameSnapshot),
                qty = x.Quantity,
                unitPrice = x.ProductPriceSnapshot,
                unitPriceUnits = BlockchainService.ToUnits(x.ProductPriceSnapshot).ToString()
            }).ToList();
        }

        // isi invoice versi JSON (sama dengan event InvoiceRecorded onchain) + hash keccak256-nya.
        // Waktu beli & kirim memakai unix timestamp dari smart contract (bukan dari kolom database),
        // supaya hash tidak berubah karena zona waktu / presisi timestamp database.
        private async Task<(string payload, string hash)> BuildInvoice(TrOrders order, ulong purchasedAtUnix, ulong shippedAtUnix)
        {
            var items = await BuildInvoiceItems(order.Id);
            var payload = JsonSerializer.Serialize(new
            {
                invoiceId = order.InvoiceId,
                orderCode = order.OrderCode,
                onchainOrderId = order.OnchainOrderId,
                seller = order.Seller?.PayoutWalletAddress,
                buyer = order.Buyer?.WalletAddress,
                items = items.Select(x => new { x.name, x.qty, price = x.unitPrice }),
                itemsTotal = order.ItemsTotal,
                shippingFee = order.ShippingFee,
                gasFeeEstimate = order.GasFeeEstimate,
                totalPrice = order.TotalIdr,
                courier = order.CourierName,
                trackingNumber = order.TrackingNumber,
                purchasedAt = purchasedAtUnix,
                shippedAt = shippedAtUnix
            });

            var hash = Sha3Keccack.Current.CalculateHash(payload).EnsureHexPrefix();
            return (payload, hash);
        }

        // seller memilih kurir + driver -> backend generate nomor resi & invoice id,
        // lalu frontend mengirim data ini ke smart contract (markShipped)
        public async Task<GetShipmentPayloadResponse> PrepareShipment(Guid sellerId, Guid orderId, PrepareShipmentRequest req)
        {
            try
            {
                var order = await _context.TrOrders.FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);
                if (order == null) return new GetShipmentPayloadResponse { Status = false, Message = "Order not found" };
                if (order.Status != StatusProcessing)
                {
                    return new GetShipmentPayloadResponse { Status = false, Message = "Please accept the order before shipping" };
                }
                if (order.SellerDeadlineAt != null && order.SellerDeadlineAt < DateTime.UtcNow)
                {
                    return new GetShipmentPayloadResponse { Status = false, Message = "The 3-day shipping deadline has passed" };
                }

                var driver = await _context.TrDrivers
                    .Include(x => x.CourierCompany)
                    .FirstOrDefaultAsync(x => x.Id == req.DriverId && x.IsActive);
                if (driver == null) return new GetShipmentPayloadResponse { Status = false, Message = "Driver not found" };

                var courier = driver.CourierCompany?.Name ?? "Courier";

                if (!string.IsNullOrWhiteSpace(order.InvoiceId) && _chain.IsEnabled)
                {
                    var onchain = await _chain.GetOrder(order.OnchainOrderId!);
                    if (onchain != null && onchain.Status != OnchainStatus.Funded)
                    {
                        return new GetShipmentPayloadResponse
                        {
                            Status = false,
                            Message = "The invoice is already recorded on-chain. Click \"Ship & record invoice\" again to finish saving the shipment."
                        };
                    }
                }

                // nomor resi dibuat sekali; jika seller ganti kurir sebelum tx berhasil, resi dibuat ulang
                if (order.DriverId != driver.Id || string.IsNullOrWhiteSpace(order.TrackingNumber))
                {
                    order.TrackingNumber = EscrowSyncService.GenerateTrackingNumber(courier);
                }
                order.InvoiceId ??= GenerateInvoiceId();
                order.DriverId = driver.Id;
                order.CourierName = courier;
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return new GetShipmentPayloadResponse
                {
                    Status = true,
                    Message = "Please record this invoice on-chain to ship the order",
                    onchainOrderId = order.OnchainOrderId,
                    invoiceId = order.InvoiceId,
                    courier = order.CourierName,
                    trackingNumber = order.TrackingNumber,
                    itemsTotal = order.ItemsTotal,
                    shippingFee = order.ShippingFee,
                    gasFeeEstimate = order.GasFeeEstimate,
                    totalPaid = order.TotalIdr,
                    items = await BuildInvoiceItems(order.Id)
                };
            }
            catch (Exception ex)
            {
                return new GetShipmentPayloadResponse { Status = false, Message = "Server error occurred while preparing shipment" };
            }
        }

        // invoice sudah tercatat onchain (markShipped) -> order dikirim
        public async Task<ResultMessageResponse> ShipOrder(Guid sellerId, Guid orderId, ShipOrderRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .Include(x => x.Buyer)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);

                if (order == null) return Fail("Order not found");
                var completingInvoice = (order.Status == StatusShipped || order.Status == StatusInTransit) &&
                                        string.IsNullOrWhiteSpace(order.InvoiceTxHash);
                if (order.Status != StatusProcessing && !completingInvoice) return Fail("Please accept the order before shipping");
                if (string.IsNullOrWhiteSpace(order.InvoiceId)) return Fail("Please prepare the shipment first");
                if (!BlockchainService.IsValidTxHash(req.InvoiceTxHash)) return Fail("Invalid invoice transaction hash");
                if (await IsTxHashUsed(req.InvoiceTxHash)) return Fail("Transaction hash is already used");

                var verify = await _chain.VerifyEscrowTx(req.InvoiceTxHash, order.OnchainOrderId!, order.Seller!.PayoutWalletAddress, EscrowEvents.InvoiceRecorded);
                if (!verify.ok) return Fail(verify.message);

                order.InvoiceTxHash = req.InvoiceTxHash.ToLower();
                var sync = await _sync.SyncOrder(order, req.InvoiceTxHash, order.Seller.UserId,
                    $"Package handed over to {order.CourierName} (tracking {order.TrackingNumber}), invoice recorded on-chain");
                if (!sync.ok) return Fail(sync.message);

                var onchainNow = await _chain.GetOrder(order.OnchainOrderId!);
                var invoice = await BuildInvoice(order, onchainNow?.FundedAt ?? 0, onchainNow?.ShippedAt ?? 0);
                order.InvoiceHash = invoice.hash;
                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Order shipped, invoice is recorded on-chain"
                };
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while shipping order");
            }
        }

        // Update Shipping status mock (seller, off-chain): shipped -> in_transit -> delivered.
        // "delivered" memulai timer buyer (default 3 hari) untuk terima / ajukan refund.
        public async Task<ResultMessageResponse> UpdateShippingStatus(Guid sellerId, Guid orderId, UpdateShippingRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .Include(x => x.Address)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);

                if (order == null) return Fail("Order not found");
                if (req.Status != StatusInTransit && req.Status != StatusDelivered)
                {
                    return Fail("Shipping status must be in_transit or delivered");
                }
                if (order.Status != StatusShipped && order.Status != StatusInTransit)
                {
                    return Fail("Order must be picked up by the courier first");
                }
                if (req.Status == StatusInTransit && order.Status != StatusShipped)
                {
                    return Fail("Order is already on the way");
                }
                if (string.IsNullOrWhiteSpace(order.InvoiceTxHash))
                {
                    return Fail("The invoice transaction is not saved yet, finish saving the shipment first");
                }

                var updatedBy = order.Seller!.UserId;
                var now = DateTime.UtcNow;
                order.Status = req.Status;
                order.UpdatedAt = now;
                order.UpdatedBy = updatedBy;

                if (req.Status == StatusDelivered)
                {
                    order.DeliveredAt = now;
                    order.BuyerDeadlineAt = now.Add(_chain.BuyerConfirmWindow);
                    _sync.AddTracking(order.Id, StatusDelivered,
                        string.IsNullOrWhiteSpace(req.Note) ? "Package arrived at destination" : req.Note,
                        req.Location ?? order.Address?.City, updatedBy);
                }
                else
                {
                    _sync.AddTracking(order.Id, StatusInTransit,
                        string.IsNullOrWhiteSpace(req.Note) ? "Package is on the way, estimated arrival: tomorrow" : req.Note,
                        req.Location, updatedBy);
                }

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = req.Status == StatusDelivered ? "Marked as arrived, the buyer can now accept or request a refund" : "Shipping status updated"
                };
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while updating shipping status");
            }
        }

        // v3: buyer terima barang dengan TANDA TANGAN EIP-712 (gratis, tanpa gas) -> status "accepted".
        // Dana belum dicairkan; seller yang mengklaimnya lewat releaseWithAcceptance (gas dibayar seller).
        public async Task<ResultMessageResponse> ConfirmDelivery(Guid userId, Guid orderId, AcceptOrderRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .Include(x => x.Buyer)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);

                if (order == null) return Fail("Order not found");
                if (order.Status == EscrowSyncService.StatusAccepted) return Fail("You have already accepted this order");
                if (order.Status != StatusShipped && order.Status != StatusInTransit && order.Status != StatusDelivered)
                {
                    return Fail("Order has not been shipped yet, is in a refund process, or is already completed");
                }
                if (!BlockchainService.IsValidSignatureHex(req.Signature)) return Fail("Invalid acceptance signature");

                var onchain = await _chain.GetOrder(order.OnchainOrderId!);
                if (onchain == null || onchain.Status != OnchainStatus.Shipped)
                {
                    // dana sudah dicairkan / di-refund lewat jalur lain -> samakan database saja
                    var synced = await _sync.SyncOrder(order, null, userId);
                    return Fail(synced.ok ? "This order is already settled on-chain" : synced.message);
                }

                // contract yang memverifikasi: penandatangan = buyer order ini, order, jumlah, chain & alamat contract
                if (!await _chain.IsValidAcceptance(order.OnchainOrderId!, req.Signature))
                {
                    return Fail("The signature is not from the buyer wallet of this order");
                }

                var now = DateTime.UtcNow;
                order.Status = EscrowSyncService.StatusAccepted;
                order.BuyerAcceptSignature = req.Signature.ToLower();
                order.AcceptedAt = now;
                order.DeliveredAt ??= now;
                order.BuyerDeadlineAt = null;
                order.BuyerRemainingSeconds = null;
                order.UpdatedAt = now;
                order.UpdatedBy = userId;
                _sync.AddTracking(order.Id, EscrowSyncService.StatusAccepted,
                    string.IsNullOrWhiteSpace(req.Note) ? "Order accepted by buyer, waiting for the seller to claim the payment" : req.Note,
                    null, userId);
                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Order accepted. The seller can now claim the payment"
                };
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while confirming delivery");
            }
        }

        // v3: seller sudah mengirim releaseWithAcceptance dari walletnya -> verifikasi event Released lalu sinkronkan
        public async Task<ResultMessageResponse> ClaimPayment(Guid sellerId, Guid orderId, ClaimPaymentRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .Include(x => x.Buyer)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);

                if (order == null) return Fail("Order not found");
                if (order.Status == EscrowSyncService.StatusCompleted) return Fail("Payment has already been claimed");
                if (order.Status != EscrowSyncService.StatusAccepted) return Fail("The buyer has not accepted this order yet");
                if (!BlockchainService.IsValidTxHash(req.TxHash)) return Fail("Invalid claim transaction hash");
                if (await IsTxHashUsed(req.TxHash)) return Fail("Transaction hash is already used");

                var verify = await _chain.VerifyEscrowTx(req.TxHash, order.OnchainOrderId!, order.Seller?.PayoutWalletAddress, EscrowEvents.Released);
                if (!verify.ok) return Fail(verify.message);

                var sync = await _sync.SyncOrder(order, req.TxHash, sellerId, "Payment claimed by seller, order completed");
                if (!sync.ok) return Fail(sync.message);

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Payment claimed, the money is now in your wallet"
                };
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while claiming payment");
            }
        }

        // Sinkronkan order dengan smart contract setelah aksi onchain lain
        // (refund oleh seller saat konfirmasi retur, refund admin, cancelExpired, claimStale, cron).
        // Boleh dipanggil buyer, seller pemilik order, atau admin.
        public async Task<ResultMessageResponse> SyncOrder(Guid loginUserId, Guid orderId, string? txHash)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .Include(x => x.Buyer)
                    .Include(x => x.Address)
                    .FirstOrDefaultAsync(x => x.Id == orderId);

                if (order == null) return Fail("Order not found");

                var isParticipant = order.BuyerId == loginUserId || order.Seller?.UserId == loginUserId;
                if (!isParticipant && !await IsAdmin(loginUserId)) return Fail("Order not found");

                if (!string.IsNullOrWhiteSpace(txHash))
                {
                    var verify = await _chain.VerifyEscrowTx(txHash, order.OnchainOrderId!, null, AllEscrowEvents);
                    if (!verify.ok) return Fail(verify.message);

                    var onchain = await _chain.GetOrder(order.OnchainOrderId!);
                    var expected = onchain == null ? Array.Empty<string>() : ExpectedEvents(onchain.Status);
                    var matches = expected.Any(sig => _chain.ReceiptHasEvent(verify.receipt!, sig, order.OnchainOrderId!));
                    if (!matches || await IsTxHashUsed(txHash)) txHash = null;
                }

                var sync = await _sync.SyncOrder(order, txHash, loginUserId);
                return new ResultMessageResponse { Status = sync.ok, Message = sync.message };
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while syncing order");
            }
        }

        // event yang menghasilkan status onchain tersebut (untuk mencatat tx hash yang benar)
        private static string[] ExpectedEvents(OnchainStatus status) => status switch
        {
            OnchainStatus.Funded => new[] { EscrowEvents.Deposited },
            OnchainStatus.Shipped => new[] { EscrowEvents.Shipped },
            OnchainStatus.Cancelled => new[] { EscrowEvents.OrderCancelled },
            OnchainStatus.Completed => new[] { EscrowEvents.Released },
            OnchainStatus.Refunded => new[] { EscrowEvents.Refunded },
            _ => Array.Empty<string>()
        };

        // cek invoice: isi JSON dibangun ulang dari database + bukti event InvoiceRecorded di tx onchain
        public async Task<GetOrderInvoiceResponse> VerifyInvoice(Guid orderId, string? invoiceHash)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .Include(x => x.Buyer)
                    .FirstOrDefaultAsync(x => x.Id == orderId);
                if (order == null)
                {
                    return new GetOrderInvoiceResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                if (order.InvoiceTxHash == null || order.InvoiceId == null)
                {
                    return new GetOrderInvoiceResponse
                    {
                        Status = false,
                        Message = "Order has not been shipped yet"
                    };
                }

                // 1. isi event InvoiceRecorded onchain (sumber kebenaran) dibandingkan field per field dengan database
                InvoiceRecordedEventDto? ev = null;
                var onchainMatch = false;
                if (_chain.IsEnabled)
                {
                    var verify = await _chain.VerifyEscrowTx(order.InvoiceTxHash, order.OnchainOrderId!, null, EscrowEvents.InvoiceRecorded);
                    ev = verify.ok && verify.receipt != null ? _chain.DecodeInvoice(verify.receipt, order.OnchainOrderId!) : null;
                    if (ev != null)
                    {
                        var dbItems = await BuildInvoiceItems(order.Id);
                        onchainMatch =
                            ev.InvoiceId == order.InvoiceId &&
                            ev.TrackingNumber == order.TrackingNumber &&
                            ev.Courier == order.CourierName &&
                            BlockchainService.SameAddress(ev.Seller, order.Seller?.PayoutWalletAddress) &&
                            ev.ItemsTotal == BlockchainService.ToUnits(order.ItemsTotal) &&
                            ev.ShippingFee == BlockchainService.ToUnits(order.ShippingFee) &&
                            ev.Items.Count == dbItems.Count &&
                            ev.Items.Zip(dbItems).All(p =>
                                p.First.Name == p.Second.name &&
                                p.First.Qty == p.Second.qty &&
                                p.First.UnitPrice.ToString() == p.Second.unitPriceUnits);
                    }
                }

                // 2. hash JSON invoice (memakai timestamp dari event onchain)
                var invoice = await BuildInvoice(order, ev?.PurchasedAt ?? 0, ev?.ShippedAt ?? 0);
                if (onchainMatch && invoice.hash != order.InvoiceHash)
                {
                    // hash lama (format sebelumnya / belum tersimpan) diperbarui karena isi onchain sudah terbukti cocok
                    order.InvoiceHash = invoice.hash;
                    await _context.SaveChangesAsync();
                }
                var compareHash = string.IsNullOrWhiteSpace(invoiceHash) ? order.InvoiceHash : invoiceHash.ToLower();
                var hashMatch = invoice.hash == compareHash || invoice.hash == order.InvoiceHash;

                var isMatch = hashMatch && onchainMatch;
                return new GetOrderInvoiceResponse
                {
                    Status = true,
                    Message = isMatch
                        ? "Invoice is valid and recorded on-chain"
                        : !onchainMatch ? "On-chain invoice does not match the order data" : "Invoice hash does not match the order data",
                    orderCode = order.OrderCode,
                    invoicePayload = invoice.payload,
                    invoiceHash = invoice.hash,
                    invoiceTxHash = order.InvoiceTxHash,
                    invoiceTxUrl = _chain.TxUrl(order.InvoiceTxHash),
                    isMatch = isMatch
                };
            }
            catch (Exception ex)
            {
                return new GetOrderInvoiceResponse
                {
                    Status = false,
                    Message = $"Server error occurred while verifying invoice"
                };
            }
        }
    }
}
