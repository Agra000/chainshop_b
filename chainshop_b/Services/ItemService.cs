//using Microsoft.EntityFrameworkCore;
//using chainshop_b.Data;
//using chainshop_b.Model;
//using chainshop_b.Model.Dto.Request;
//using chainshop_b.Model.Dto.Response;

//namespace chainshop_b.Services
//{
//    public class ItemService
//    {
//        private readonly ApplicationDBContext _context;

//        public ItemService(ApplicationDBContext context)
//        {
//            _context = context;
//        }

//        public async Task<List<TrItem>> GetAllProducts()
//        {
//            try
//            {
//                return await _context.TrItem.Select(x => new TrItem
//                {
//                    Id = x.Id,
//                    ItemName = x.ItemName,
//                    Brand = x.Brand,
//                    Category = x.Category,
//                    Price = x.Price,
//                    StokQty = x.StokQty,
//                    ImageUrl = x.ImageUrl,
//                    Discount = x.Discount,
//                    DateIn = x.DateIn,
//                }).ToListAsync();
//            }
//            catch (Exception ex)
//            {
//                return new List<TrItem>();
//            }
//        }

//        public async Task<ResultMessageResponse> AddNewItem(Guid userId, FormItemsRequest req)
//        {
//            try
//            {
//                var newItem = new TrItem
//                {
//                    OwnerId = Guid.NewGuid(),
//                    ItemName = req.ItemName,
//                    Brand = req.Brand,
//                    Category = req.Category,
//                    Size = req.Size,
//                    Color = req.Color,
//                    Price = req.Price,
//                    StokQty = req.StockQty,
//                    Description = req.Description ?? string.Empty,
//                    ImageUrl = req.ImageURL,
//                    Discount = req.Discount,
//                    DateIn = DateTime.UtcNow,
//                    UserIn = Guid.NewGuid()
//                };

//                _context.TrItem.Add(newItem);
//                await _context.SaveChangesAsync();

//                return new ResultMessageResponse
//                {
//                    Status = true,
//                    Message = "Item added successfully"
//                };
//            }
//            catch (Exception ex)
//            {
//                return new ResultMessageResponse
//                {
//                    Status = false,
//                    Message = $"Server error occurred while adding item"
//                };
//            }
//        }
//    }
//}
