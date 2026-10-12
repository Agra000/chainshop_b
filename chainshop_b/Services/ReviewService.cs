using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    public class ReviewService
    {
        private readonly ApplicationDBContext _context;

        public ReviewService(ApplicationDBContext context)
        {
            _context = context;
        }

        public async Task<ResultMessageResponse> AddReview(Guid userId, ReviewRequest req)
        {
            try
            {
                if (req.Rating < 1 || req.Rating > 5)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Rating must be between 1 and 5"
                    };
                }

                var orderItem = await _context.TrOrderItems
                    .Include(x => x.Order)
                    .FirstOrDefaultAsync(x => x.Id == req.OrderItemId);

                if (orderItem == null || orderItem.Order == null || orderItem.Order.BuyerId != userId)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Order item not found"
                    };
                }

                // rating hanya bisa setelah delivery confirmation (order completed)
                if (orderItem.Order.Status != "completed")
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "You can only rate items after confirming the delivery"
                    };
                }

                var alreadyReviewed = await _context.TrProductReviews.AnyAsync(x => x.OrderItemId == req.OrderItemId);
                if (alreadyReviewed)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "You have already rated this item"
                    };
                }

                var newReview = new TrProductReviews
                {
                    OrderItemId = orderItem.Id,
                    ProductId = orderItem.ProductId,
                    BuyerId = userId,
                    Rating = req.Rating,
                    Comment = req.Comment,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId
                };
                _context.TrProductReviews.Add(newReview);

                // update rating product
                var product = await _context.TrProducts.FirstOrDefaultAsync(x => x.Id == orderItem.ProductId);
                if (product != null)
                {
                    product.RatingAvg = Math.Round(((product.RatingAvg * product.RatingCount) + req.Rating) / (product.RatingCount + 1), 1);
                    product.RatingCount += 1;
                    product.UpdatedAt = DateTime.UtcNow;
                    product.UpdatedBy = userId;
                }

                // update rating seller
                var seller = await _context.MsSellers.FirstOrDefaultAsync(x => x.Id == orderItem.Order.SellerId);
                if (seller != null)
                {
                    seller.RatingAvg = Math.Round(((seller.RatingAvg * seller.RatingCount) + req.Rating) / (seller.RatingCount + 1), 1);
                    seller.RatingCount += 1;
                    seller.UpdatedAt = DateTime.UtcNow;
                    seller.UpdatedBy = userId;
                }

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Thank you for rating this item"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while adding review"
                };
            }
        }

        public async Task<List<GetReviewResponse>> GetProductReviews(Guid productId)
        {
            try
            {
                return await _context.TrProductReviews
                    .Where(x => x.ProductId == productId)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new GetReviewResponse
                    {
                        Id = x.Id,
                        productId = x.ProductId,
                        buyerName = x.Buyer == null ? "" : x.Buyer.Name,
                        rating = x.Rating,
                        comment = x.Comment,
                        createdAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetReviewResponse>();
            }
        }
    }
}
