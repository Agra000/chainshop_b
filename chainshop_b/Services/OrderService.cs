using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace chainshop_b.Services
{
    public class OrderService
    {
        private readonly ApplicationDBContext _context;

        // alur status order (sesuai activity diagram)
        // pending_payment -> paid -> processing -> shipped -> in_transit -> delivered -> completed
        // pending_payment -> cancelled (buyer cancel), paid/processing -> cancelled (seller reject + refund escrow)
        private const string StatusPendingPayment = "pending_payment";
        private const string StatusPaid = "paid";
        private const string StatusProcessing = "processing";
        private const string StatusShipped = "shipped";
        private const string StatusInTransit = "in_transit";
        private const string StatusDelivered = "delivered";
        private const string StatusCompleted = "completed";
        private const string StatusCancelled = "cancelled";

        private const string EscrowAddress = "escrow_contract";

        public OrderService(ApplicationDBContext context)
        {
            _context = context;
        }

        private static bool IsValidTxHash(string? txHash)
        {
            return !string.IsNullOrWhiteSpace(txHash) && Regex.IsMatch(txHash, "^0x[0-9a-fA-F]{64}$");
        }

        private static string GenerateOrderCode()
        {
            return $"CS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        }

        private static string FormatDate(DateTime? date)
        {
            return date == null ? "" : date.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void AddTracking(Guid orderId, string status, string? note, string? location, Guid updatedBy)
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

        private async Task<bool> IsTxHashUsed(string txHash)
        {
            var hash = txHash.ToLower();
            return await _context.TrEscrowTransactions.AnyAsync(x => x.TxHash == hash) ||
                   await _context.TrOrders.AnyAsync(x => x.InvoiceTxHash == hash);
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

        // isi invoice yang di-hash lalu disimpan onchain oleh seller,
        // dipakai untuk membuktikan barang yang dikirim sesuai dengan pesanan
        private async Task<(string payload, string hash)> BuildInvoice(TrOrders order, Guid driverId, string trackingNumber)
        {
            var items = await _context.TrOrderItems
                .Where(x => x.OrderId == order.Id)
                .OrderBy(x => x.ProductId)
                .Select(x => new
                {
                    productId = x.ProductId,
                    name = x.ProductNameSnapshot,
                    price = x.ProductPriceSnapshot,
                    qty = x.Quantity,
                    lineTotal = x.LineTotalIdr
                }).ToListAsync();

            var payload = JsonSerializer.Serialize(new
            {
                orderCode = order.OrderCode,
                buyerId = order.BuyerId,
                sellerId = order.SellerId,
                addressId = order.AddressId,
                driverId = driverId,
                trackingNumber = trackingNumber.Trim(),
                totalIdr = order.TotalIdr,
                items = items
            });

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return (payload, "0x" + Convert.ToHexString(bytes).ToLower());
        }

        public async Task<CheckoutResponse> Checkout(Guid userId, CheckoutRequest req)
        {
            await using var trx = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.MsUsers.FirstOrDefaultAsync(x => x.Id == userId);
                if (user == null)
                {
                    return new CheckoutResponse
                    {
                        Status = false,
                        Message = "User not found"
                    };
                }

                var address = await _context.MsAddresses.FirstOrDefaultAsync(x => x.Id == req.AddressId && x.UserId == userId);
                if (address == null)
                {
                    return new CheckoutResponse
                    {
                        Status = false,
                        Message = "Shipping address not found"
                    };
                }

                var cartItems = await _context.TrCartItems
                    .Include(x => x.Product)
                    .ThenInclude(x => x!.Seller)
                    .Where(x => x.UserId == userId && x.IsSelected && x.Quantity > 0)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    return new CheckoutResponse
                    {
                        Status = false,
                        Message = "No item selected in cart"
                    };
                }

                foreach (var item in cartItems)
                {
                    if (item.Product == null || item.Product.Status != "active")
                    {
                        return new CheckoutResponse
                        {
                            Status = false,
                            Message = "Some items are no longer available"
                        };
                    }

                    if (item.Product.Stock < item.Quantity)
                    {
                        return new CheckoutResponse
                        {
                            Status = false,
                            Message = $"Insufficient stock for {item.Product.Name}"
                        };
                    }

                    if (item.Product.Seller != null && item.Product.Seller.UserId == userId)
                    {
                        return new CheckoutResponse
                        {
                            Status = false,
                            Message = "You cannot buy your own product"
                        };
                    }
                }

                var result = new CheckoutResponse
                {
                    Status = true,
                    Message = "Order created, please continue the payment"
                };

                // 1 order per seller, karena escrow di-release ke wallet masing-masing seller
                foreach (var group in cartItems.GroupBy(x => x.Product!.SellerId))
                {
                    var seller = group.First().Product!.Seller!;
                    var order = new TrOrders
                    {
                        OrderCode = GenerateOrderCode(),
                        BuyerId = userId,
                        SellerId = group.Key,
                        AddressId = address.Id,
                        TotalIdr = group.Sum(x => x.Product!.PriceIdr * x.Quantity),
                        Status = StatusPendingPayment,
                        PaymentMethod = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "crypto" : req.PaymentMethod,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = userId
                    };
                    _context.TrOrders.Add(order);

                    foreach (var item in group)
                    {
                        _context.TrOrderItems.Add(new TrOrderItems
                        {
                            OrderId = order.Id,
                            ProductId = item.ProductId,
                            ProductNameSnapshot = item.Product!.Name,
                            ProductPriceSnapshot = item.Product.PriceIdr,
                            Quantity = item.Quantity,
                            LineTotalIdr = item.Product.PriceIdr * item.Quantity,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = userId
                        });

                        // stock di-reserve saat checkout, dikembalikan jika order dibatalkan
                        item.Product.Stock -= item.Quantity;
                        item.Product.UpdatedAt = DateTime.UtcNow;
                        item.Product.UpdatedBy = userId;
                    }

                    AddTracking(order.Id, StatusPendingPayment, "Waiting for payment", null, userId);

                    result.orders.Add(new CheckoutOrderResponse
                    {
                        orderId = order.Id,
                        orderCode = order.OrderCode,
                        sellerId = seller.Id,
                        shopName = seller.ShopName,
                        sellerWallet = seller.PayoutWalletAddress,
                        totalIdr = order.TotalIdr
                    });
                }

                _context.TrCartItems.RemoveRange(cartItems);

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

        // Payment Verification + Holding buyer money (escrow hold onchain)
        public async Task<ResultMessageResponse> PayOrder(Guid userId, Guid orderId, EscrowTxRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Buyer)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);

                if (order == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                if (order.Status != StatusPendingPayment)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order is already paid or cancelled"
                    };
                }

                if (!IsValidTxHash(req.TxHash))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Invalid transaction hash"
                    };
                }

                if (await IsTxHashUsed(req.TxHash))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Transaction hash is already used"
                    };
                }

                _context.TrEscrowTransactions.Add(new TrEscrowTransactions
                {
                    OrderId = order.Id,
                    TxType = "hold",
                    AmountIdr = order.TotalIdr,
                    TxHash = req.TxHash.ToLower(),
                    FromWallet = req.WalletAddress ?? order.Buyer?.WalletAddress,
                    ToWallet = EscrowAddress,
                    Status = "success",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId
                });

                // Make an order -> order masuk ke order list seller
                order.Status = StatusPaid;
                order.PaidAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = userId;

                AddTracking(order.Id, StatusPaid, "Payment verified, money is held in escrow", null, userId);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Payment verified, your money is safely held in escrow"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while verifying payment"
                };
            }
        }

        public async Task<ResultMessageResponse> CancelOrder(Guid userId, Guid orderId)
        {
            try
            {
                var order = await _context.TrOrders.FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);
                if (order == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                if (order.Status != StatusPendingPayment)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Only unpaid order can be cancelled"
                    };
                }

                await RestoreStock(order.Id, userId);

                order.Status = StatusCancelled;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = userId;

                AddTracking(order.Id, StatusCancelled, "Order cancelled by buyer", null, userId);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Order cancelled successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while cancelling order"
                };
            }
        }

        public async Task<List<GetOrderResponse>> GetBuyerOrders(Guid userId, string? status)
        {
            try
            {
                var query = _context.TrOrders.Where(x => x.BuyerId == userId);
                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(x => x.Status == status);
                }

                return await query
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
                        createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToListAsync();
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
                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(x => x.Status == status);
                }

                return await query
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
                        createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetOrderResponse>();
            }
        }

        // detail order + tracking delivery, bisa diakses buyer (userId) atau seller (sellerId)
        public async Task<GetOrderDetailResponse> GetOrderDetail(Guid orderId, Guid? userId, Guid? sellerId)
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

                if (order == null || (order.BuyerId != userId && order.SellerId != sellerId))
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
                    escrowTransactions = escrows
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

        // seller menerima order dan mulai memproses barang
        public async Task<ResultMessageResponse> ProcessOrder(Guid sellerId, Guid orderId)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);

                if (order == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                if (order.Status != StatusPaid)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Only paid order can be processed"
                    };
                }

                var updatedBy = order.Seller!.UserId;
                order.Status = StatusProcessing;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = updatedBy;

                AddTracking(order.Id, StatusProcessing, "Seller is processing your order", order.Seller.City, updatedBy);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Order is now being processed"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while processing order"
                };
            }
        }

        // seller menolak order, uang di escrow dikembalikan ke buyer (refund onchain)
        public async Task<ResultMessageResponse> RejectOrder(Guid sellerId, Guid orderId, EscrowTxRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .Include(x => x.Buyer)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);

                if (order == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                if (order.Status != StatusPaid && order.Status != StatusProcessing)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order can no longer be rejected"
                    };
                }

                if (!IsValidTxHash(req.TxHash))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Invalid refund transaction hash"
                    };
                }

                if (await IsTxHashUsed(req.TxHash))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Transaction hash is already used"
                    };
                }

                var updatedBy = order.Seller!.UserId;

                _context.TrEscrowTransactions.Add(new TrEscrowTransactions
                {
                    OrderId = order.Id,
                    TxType = "refund",
                    AmountIdr = order.TotalIdr,
                    TxHash = req.TxHash.ToLower(),
                    FromWallet = EscrowAddress,
                    ToWallet = order.Buyer?.WalletAddress,
                    Status = "success",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = updatedBy
                });

                await RestoreStock(order.Id, updatedBy);

                order.Status = StatusCancelled;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = updatedBy;

                AddTracking(order.Id, StatusCancelled, string.IsNullOrWhiteSpace(req.Note) ? "Order rejected by seller, money refunded to buyer" : req.Note, null, updatedBy);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Order rejected and money refunded to buyer"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while rejecting order"
                };
            }
        }

        // generate invoice + hash yang harus disimpan seller ke onchain sebelum barang dikirim
        public async Task<GetOrderInvoiceResponse> GetShippingInvoice(Guid sellerId, Guid orderId, Guid driverId, string trackingNumber)
        {
            try
            {
                var order = await _context.TrOrders.FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);
                if (order == null)
                {
                    return new GetOrderInvoiceResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                if (order.Status != StatusProcessing)
                {
                    return new GetOrderInvoiceResponse
                    {
                        Status = false,
                        Message = "Please process the order before shipping"
                    };
                }

                if (string.IsNullOrWhiteSpace(trackingNumber))
                {
                    return new GetOrderInvoiceResponse
                    {
                        Status = false,
                        Message = "Tracking number is required"
                    };
                }

                var driver = await _context.TrDrivers.AnyAsync(x => x.Id == driverId && x.IsActive);
                if (!driver)
                {
                    return new GetOrderInvoiceResponse
                    {
                        Status = false,
                        Message = "Driver not found"
                    };
                }

                var invoice = await BuildInvoice(order, driverId, trackingNumber);

                return new GetOrderInvoiceResponse
                {
                    Status = true,
                    Message = "Please store this invoice hash onchain before shipping",
                    orderCode = order.OrderCode,
                    invoicePayload = invoice.payload,
                    invoiceHash = invoice.hash
                };
            }
            catch (Exception ex)
            {
                return new GetOrderInvoiceResponse
                {
                    Status = false,
                    Message = $"Server error occurred while generating invoice"
                };
            }
        }

        // Process the items and send it to shipper (invoice sudah tersimpan onchain)
        public async Task<ResultMessageResponse> ShipOrder(Guid sellerId, Guid orderId, ShipOrderRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);

                if (order == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                if (order.Status != StatusProcessing)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Please process the order before shipping"
                    };
                }

                if (string.IsNullOrWhiteSpace(req.TrackingNumber))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Tracking number is required"
                    };
                }

                var driver = await _context.TrDrivers
                    .Include(x => x.CourierCompany)
                    .FirstOrDefaultAsync(x => x.Id == req.DriverId && x.IsActive);

                if (driver == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Driver not found"
                    };
                }

                if (!IsValidTxHash(req.InvoiceTxHash))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Invalid invoice transaction hash"
                    };
                }

                if (await IsTxHashUsed(req.InvoiceTxHash))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Transaction hash is already used"
                    };
                }

                var invoice = await BuildInvoice(order, driver.Id, req.TrackingNumber);
                var updatedBy = order.Seller!.UserId;

                order.DriverId = driver.Id;
                order.TrackingNumber = req.TrackingNumber.Trim();
                order.InvoiceHash = invoice.hash;
                order.InvoiceTxHash = req.InvoiceTxHash.ToLower();
                order.Status = StatusShipped;
                order.ShippedAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = updatedBy;

                AddTracking(order.Id, StatusShipped, $"Package handed over to {driver.CourierCompany?.Name ?? "courier"} ({driver.FullName})", order.Seller.City, updatedBy);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Order shipped, invoice is recorded onchain"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while shipping order"
                };
            }
        }

        // Update Shipping status (seller / kurir)
        public async Task<ResultMessageResponse> UpdateShippingStatus(Guid sellerId, Guid orderId, UpdateShippingRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.SellerId == sellerId);

                if (order == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                var shippingStatus = new[] { StatusShipped, StatusInTransit, StatusDelivered };
                if (!shippingStatus.Contains(order.Status))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order is not on shipping"
                    };
                }

                if (req.Status != StatusInTransit && req.Status != StatusDelivered)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Shipping status must be in_transit or delivered"
                    };
                }

                if (order.Status == StatusDelivered && req.Status == StatusInTransit)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order is already delivered"
                    };
                }

                var updatedBy = order.Seller!.UserId;

                order.Status = req.Status;
                if (req.Status == StatusDelivered)
                {
                    order.DeliveredAt = DateTime.UtcNow;
                }
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = updatedBy;

                AddTracking(order.Id, req.Status,
                    string.IsNullOrWhiteSpace(req.Note) ? (req.Status == StatusDelivered ? "Package arrived at destination" : "Package is on the way") : req.Note,
                    req.Location, updatedBy);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Shipping status updated"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while updating shipping status"
                };
            }
        }

        // Delivery Confirmation -> Release Money (escrow release onchain) -> Save Order transaction
        public async Task<ResultMessageResponse> ConfirmDelivery(Guid userId, Guid orderId, EscrowTxRequest req)
        {
            try
            {
                var order = await _context.TrOrders
                    .Include(x => x.Seller)
                    .FirstOrDefaultAsync(x => x.Id == orderId && x.BuyerId == userId);

                if (order == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                var shippingStatus = new[] { StatusShipped, StatusInTransit, StatusDelivered };
                if (!shippingStatus.Contains(order.Status))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order has not been shipped yet or is already completed"
                    };
                }

                if (!IsValidTxHash(req.TxHash))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Invalid release transaction hash"
                    };
                }

                if (await IsTxHashUsed(req.TxHash))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Transaction hash is already used"
                    };
                }

                _context.TrEscrowTransactions.Add(new TrEscrowTransactions
                {
                    OrderId = order.Id,
                    TxType = "release",
                    AmountIdr = order.TotalIdr,
                    TxHash = req.TxHash.ToLower(),
                    FromWallet = EscrowAddress,
                    ToWallet = order.Seller?.PayoutWalletAddress,
                    Status = "success",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId
                });

                // barang sudah benar-benar diterima, tambahkan sold count
                var items = await _context.TrOrderItems.Where(x => x.OrderId == order.Id).ToListAsync();
                var productIds = items.Select(x => x.ProductId).ToList();
                var products = await _context.TrProducts.Where(x => productIds.Contains(x.Id)).ToListAsync();
                foreach (var item in items)
                {
                    var product = products.FirstOrDefault(x => x.Id == item.ProductId);
                    if (product != null)
                    {
                        product.SoldCount += item.Quantity;
                        product.UpdatedAt = DateTime.UtcNow;
                        product.UpdatedBy = userId;
                    }
                }

                order.Status = StatusCompleted;
                order.DeliveredAt ??= DateTime.UtcNow;
                order.CompletedAt = DateTime.UtcNow;
                order.UpdatedAt = DateTime.UtcNow;
                order.UpdatedBy = userId;

                AddTracking(order.Id, StatusCompleted, string.IsNullOrWhiteSpace(req.Note) ? "Delivery confirmed by buyer, money released to seller" : req.Note, null, userId);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Delivery confirmed, money has been released to seller"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while confirming delivery"
                };
            }
        }

        // cek apakah hash invoice onchain sama dengan data order di database
        public async Task<GetOrderInvoiceResponse> VerifyInvoice(Guid orderId, string? invoiceHash)
        {
            try
            {
                var order = await _context.TrOrders.FirstOrDefaultAsync(x => x.Id == orderId);
                if (order == null)
                {
                    return new GetOrderInvoiceResponse
                    {
                        Status = false,
                        Message = "Order not found"
                    };
                }

                if (order.DriverId == null || order.TrackingNumber == null || order.InvoiceHash == null)
                {
                    return new GetOrderInvoiceResponse
                    {
                        Status = false,
                        Message = "Order has not been shipped yet"
                    };
                }

                var invoice = await BuildInvoice(order, order.DriverId.Value, order.TrackingNumber);
                var compareHash = string.IsNullOrWhiteSpace(invoiceHash) ? order.InvoiceHash : invoiceHash.ToLower();
                var isMatch = invoice.hash == compareHash && invoice.hash == order.InvoiceHash;

                return new GetOrderInvoiceResponse
                {
                    Status = true,
                    Message = isMatch ? "Invoice is valid" : "Invoice does not match the order data",
                    orderCode = order.OrderCode,
                    invoicePayload = invoice.payload,
                    invoiceHash = invoice.hash,
                    invoiceTxHash = order.InvoiceTxHash,
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
