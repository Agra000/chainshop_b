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

        public const int MaxCommentLength = 500;
        public const int MaxItemsPerRequest = 50;

        public ReviewService(ApplicationDBContext context)
        {
            _context = context;
        }

        private static ResultMessageResponse Fail(string message) => new() { Status = false, Message = message };

        // Order boleh di-rating jika sudah selesai (completed) DAN dana escrow sudah dilepas ke seller
        // (catatan "release" ada di trescrowtransactions). Berlaku untuk semua jalur completed:
        // buyer tanda tangan terima + seller klaim, auto-release cron, refund ditolak/kedaluwarsa, claimStale.
        public async Task<bool> IsOrderReviewable(TrOrders order)
        {
            if (order.Status != EscrowSyncService.StatusCompleted) return false;
            return await _context.TrEscrowTransactions.AnyAsync(x => x.OrderId == order.Id && x.TxType == "release");
        }

        // rating satu barang (dipakai halaman detail transaksi)
        public async Task<ResultMessageResponse> AddReview(Guid userId, ReviewRequest req)
        {
            try
            {
                var orderItem = await _context.TrOrderItems
                    .Include(x => x.Order)
                    .FirstOrDefaultAsync(x => x.Id == req.OrderItemId);

                if (orderItem == null || orderItem.Order == null || orderItem.Order.BuyerId != userId)
                {
                    return Fail("Order item not found");
                }

                return await SaveReviews(userId, orderItem.Order,
                    new List<(TrOrderItems item, short rating, string? comment)> { (orderItem, req.Rating, req.Comment) });
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while adding review");
            }
        }

        // rating beberapa barang dalam satu order sekaligus (dipakai halaman Order History)
        public async Task<ResultMessageResponse> AddOrderReviews(Guid userId, OrderReviewRequest req)
        {
            try
            {
                if (req.Items == null || req.Items.Count == 0) return Fail("Please rate at least one item");
                if (req.Items.Count > MaxItemsPerRequest) return Fail("Too many items in one request");
                if (req.Items.Select(x => x.OrderItemId).Distinct().Count() != req.Items.Count)
                {
                    return Fail("Each item can only be rated once");
                }

                var order = await _context.TrOrders.FirstOrDefaultAsync(x => x.Id == req.OrderId && x.BuyerId == userId);
                if (order == null) return Fail("Order not found");

                var itemIds = req.Items.Select(x => x.OrderItemId).ToList();
                var orderItems = await _context.TrOrderItems
                    .Where(x => x.OrderId == order.Id && itemIds.Contains(x.Id))
                    .ToListAsync();
                if (orderItems.Count != itemIds.Count) return Fail("Order item not found");

                var entries = req.Items
                    .Select(r => (orderItems.First(i => i.Id == r.OrderItemId), r.Rating, r.Comment))
                    .ToList();

                return await SaveReviews(userId, order, entries);
            }
            catch (Exception ex)
            {
                return Fail("Server error occurred while adding review");
            }
        }

        private async Task<ResultMessageResponse> SaveReviews(
            Guid userId, TrOrders order, List<(TrOrderItems item, short rating, string? comment)> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.rating < 1 || entry.rating > 5) return Fail("Rating must be between 1 and 5");
                if ((entry.comment?.Trim().Length ?? 0) > MaxCommentLength)
                {
                    return Fail($"Comment can be at most {MaxCommentLength} characters");
                }
            }

            if (!await IsOrderReviewable(order))
            {
                return Fail("You can only rate items after the order is completed and the payment has been released to the seller");
            }

            var itemIds = entries.Select(x => x.item.Id).ToList();
            if (await _context.TrProductReviews.AnyAsync(x => itemIds.Contains(x.OrderItemId)))
            {
                return Fail(entries.Count == 1 ? "You have already rated this item" : "Some of these items have already been rated");
            }

            var now = DateTime.UtcNow;
            foreach (var entry in entries)
            {
                _context.TrProductReviews.Add(new TrProductReviews
                {
                    OrderItemId = entry.item.Id,
                    ProductId = entry.item.ProductId,
                    BuyerId = userId,
                    Rating = entry.rating,
                    Comment = string.IsNullOrWhiteSpace(entry.comment) ? null : entry.comment.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    UpdatedBy = userId
                });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // unique index ux_trproductreviews_order_item_id: request ganda yang masuk bersamaan
                return Fail(entries.Count == 1 ? "You have already rated this item" : "Some of these items have already been rated");
            }

            await RecalculateRatings(entries.Select(x => x.item.ProductId).Distinct().ToList(), order.SellerId, userId);

            return new ResultMessageResponse
            {
                Status = true,
                Message = entries.Count == 1 ? "Thank you for rating this item" : $"Thank you for rating {entries.Count} items"
            };
        }

        // Rating dihitung ulang dari SEMUA review di database (rata-rata + jumlah dari seluruh buyer),
        // bukan ditambah bertahap, supaya tidak ada selisih pembulatan yang menumpuk.
        // Satu statement UPDATE per baris, jadi dua buyer yang memberi rating bersamaan tetap menghasilkan nilai yang benar.
        public async Task RecalculateRatings(IEnumerable<Guid> productIds, Guid? sellerId, Guid updatedBy)
        {
            foreach (var productId in productIds)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE trproducts p
                    SET rating_count = s.cnt,
                        rating_avg = s.avg,
                        updated_at = now(),
                        updated_by = {updatedBy}
                    FROM (
                        SELECT COUNT(*)::int AS cnt, COALESCE(ROUND(AVG(r.rating), 1), 0) AS avg
                        FROM trproductreviews r
                        WHERE r.product_id = {productId}
                    ) s
                    WHERE p.id = {productId}");
            }

            if (sellerId != null)
            {
                // rating toko = rata-rata semua review barang dari order toko tersebut
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE mssellers sl
                    SET rating_count = s.cnt,
                        rating_avg = s.avg,
                        updated_at = now(),
                        updated_by = {updatedBy}
                    FROM (
                        SELECT COUNT(*)::int AS cnt, COALESCE(ROUND(AVG(r.rating), 1), 0) AS avg
                        FROM trproductreviews r
                        JOIN trorderitems oi ON oi.id = r.order_item_id
                        JOIN trorders o ON o.id = oi.order_id
                        WHERE o.seller_id = {sellerId.Value}
                    ) s
                    WHERE sl.id = {sellerId.Value}");
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
