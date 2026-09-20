using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    public class ProductService
    {
        private readonly ApplicationDBContext _context;

        public ProductService(ApplicationDBContext context)
        {
            _context = context;
        }

        public async Task<List<GetProductResponse>> GetAllProducts()
        {
            try
            {
                return await _context.TrProducts.Select(x => new GetProductResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    CategoryName = x.Category == null ? string.Empty : x.Category.Name,
                    PriceIdr = x.PriceIdr,
                    Stock = x.Stock,
                    Slug = x.Slug,
                    RatingAvg = x.RatingAvg,
                    Description = x.Description,
                    SellerName = x.Seller == null ? string.Empty : x.Seller.ShopName,
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetProductResponse>();
            }
        }

        public async Task<ResultMessageResponse> UpsertProduct(Guid sellerId, FormItemsRequest req)
        {
            try
            {
                if (req.Stock < 1)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Insufficient stock"
                    };
                }

                var seller = await _context.MsSellers.AnyAsync(x => x.Id == sellerId);
                if (!seller)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "User is not a seller"
                    };
                }

                var categoryExists = await _context.MsCategories.AnyAsync(x => x.Id == req.CategoryId);
                if (!categoryExists)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Category not found"
                    };
                }

                var itemExists = await _context.TrProducts.FirstOrDefaultAsync(x =>
                        x.SellerId == sellerId &&
                        x.Name == req.Name &&
                        x.CategoryId == req.CategoryId);

                if (itemExists != null)
                {
                    itemExists.PriceIdr = req.PriceIdr;
                    itemExists.Stock += req.Stock;
                    itemExists.Description = req.Description;
                    itemExists.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var newItem = new TrProducts
                    {
                        SellerId = sellerId,
                        Name = req.Name,
                        CategoryId = req.CategoryId,
                        PriceIdr = req.PriceIdr,
                        Stock = req.Stock,
                        Description = req.Description ?? string.Empty,
                        Slug = req.Slug,
                        CreatedAt = DateTime.UtcNow,
                    };

                    _context.TrProducts.Add(newItem);
                }

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Product added successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while adding item"
                };
            }
        }
    }
}
