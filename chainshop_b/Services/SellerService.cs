using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    public class SellerService
    {
        private readonly ApplicationDBContext _context;

        public SellerService(ApplicationDBContext context)
        {
            _context = context;
        }

        public async Task<ResultMessageResponse> BecomeSeller(Guid userId, BecomeSellerRequest req)
        {
            try
            {
                var user = await _context.MsUsers.FirstOrDefaultAsync(x => x.Id == userId);

                if (user == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "User not found"
                    };
                }

                if (user.WalletAddress == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Invalid wallet address"
                    };
                }

                var existingSeller = await _context.MsSellers.FirstOrDefaultAsync(x => x.UserId == userId);
                if (existingSeller != null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "You are already a seller"
                    };
                }

                req.PayoutWalletAddress = user.WalletAddress;

                var newSeller = new MsSellers
                {
                    UserId = userId,
                    ShopName = req.ShopName,
                    Image = req.Image,
                    ShopDescription = req.ShopDescription ?? string.Empty,
                    City = req.City ?? string.Empty,
                    PayoutWalletAddress = req.PayoutWalletAddress,
                    IsVerified = false,
                    RatingAvg = 0,
                    RatingCount = 0,
                    UpdatedAt = DateTime.UtcNow,
                };

                _context.MsSellers.Add(newSeller);
                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = $"Welcome to seller family {user.Name}!"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while adding seller"
                };
            }
        }

        public async Task<GetSellerInfoResponse> GetSellerInfo(Guid sellerId)
        {
            try
            {
                var seller = await _context.MsSellers.FirstOrDefaultAsync(x => x.Id == sellerId);
                if (seller == null)
                {
                    return new GetSellerInfoResponse
                    {
                        Status = false,
                        Message = "Seller not found"
                    };
                }

                var user = await _context.MsUsers.FirstOrDefaultAsync(x => x.Id == seller.UserId);
                if (user == null)
                {
                    return new GetSellerInfoResponse
                    {
                        Status = false,
                        Message = "User not found"
                    };
                }

                return new GetSellerInfoResponse
                {
                    Status = true,
                    shopName = seller.ShopName,
                    shopDescription = seller.ShopDescription,
                    city = seller.City,
                    payoutWallet = seller.PayoutWalletAddress,
                    registeredAt = seller.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    shopImage = seller.Image
                };
            }
            catch (Exception ex)
            {
                return new GetSellerInfoResponse
                {
                    Status = false,
                    Message = $"Server error occurred while adding seller"
                };
            }
        }

        public async Task<GetSellerDashboardResponse> GetSellerDashboard(Guid sellerId)
        {
            try
            {
                var seller = await _context.MsSellers.FirstOrDefaultAsync(x => x.Id == sellerId);
                if (seller == null)
                {
                    return new GetSellerDashboardResponse
                    {
                        Status = false,
                        Message = "Seller not found"
                    };
                }

                var orders = _context.TrOrders.Where(x => x.SellerId == sellerId && x.Status != "pending_payment");
                var heldStatus = new[] { "paid", "processing", "shipped", "in_transit", "delivered" };
                var shippingStatus = new[] { "shipped", "in_transit", "delivered" };

                var products = await _context.TrProducts
                    .Where(x => x.SellerId == sellerId)
                    .OrderByDescending(x => x.UpdatedAt)
                    .Select(x => new GetProductResponse
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Category = x.Category == null ? string.Empty : x.Category.Name,
                        Price = x.PriceIdr,
                        Sold = x.SoldCount,
                        Stock = x.Stock,
                        Image = x.Image,
                        RatingAvg = x.RatingAvg,
                        Description = x.Description,
                        Seller = seller.ShopName,
                    }).ToListAsync();

                return new GetSellerDashboardResponse
                {
                    Status = true,
                    Message = "Success",
                    shopName = seller.ShopName,
                    ratingAvg = seller.RatingAvg,
                    ratingCount = seller.RatingCount,
                    totalProducts = products.Count,
                    totalSold = products.Sum(x => x.Sold),
                    totalOrders = await orders.CountAsync(),
                    newOrders = await orders.CountAsync(x => x.Status == "paid" || x.Status == "processing"),
                    onShippingOrders = await orders.CountAsync(x => shippingStatus.Contains(x.Status)),
                    completedOrders = await orders.CountAsync(x => x.Status == "completed"),
                    escrowHeldIdr = await orders.Where(x => heldStatus.Contains(x.Status)).SumAsync(x => x.TotalIdr),
                    totalRevenueIdr = await _context.TrEscrowTransactions
                        .Where(x => x.TxType == "release" && x.Status == "success" && x.Order != null && x.Order.SellerId == sellerId)
                        .SumAsync(x => x.AmountIdr),
                    products = products
                };
            }
            catch (Exception ex)
            {
                return new GetSellerDashboardResponse
                {
                    Status = false,
                    Message = $"Server error occurred while getting seller dashboard"
                };
            }
        }
    }
}
