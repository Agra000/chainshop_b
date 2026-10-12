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
                    Category = x.Category == null ? string.Empty : x.Category.Name,
                    Price = x.PriceIdr,
                    Sold = x.SoldCount,
                    Stock = x.Stock,
                    Image = x.Image,
                    RatingAvg = x.RatingAvg,
                    Description = x.Description,
                    Seller = x.Seller == null ? string.Empty : x.Seller.ShopName,
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
                        Image = req.Image,
                        UpdatedAt = DateTime.UtcNow,
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

        public async Task<List<GetProductResponse>> SearchProducts(string? keyword, Guid? categoryId, string? categorySlug, long? minPrice, long? maxPrice, string? sortBy)
        {
            try
            {
                var query = _context.TrProducts.Where(x => x.Status == "active");

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var key = keyword.Trim().ToLower();
                    query = query.Where(x => x.Name.ToLower().Contains(key) ||
                                             (x.Description != null && x.Description.ToLower().Contains(key)));
                }

                if (categoryId.HasValue && categoryId.Value != Guid.Empty)
                {
                    query = query.Where(x => x.CategoryId == categoryId.Value);
                }

                if (!string.IsNullOrWhiteSpace(categorySlug))
                {
                    query = query.Where(x => x.Category != null && x.Category.Slug == categorySlug);
                }

                if (minPrice.HasValue)
                {
                    query = query.Where(x => x.PriceIdr >= minPrice.Value);
                }

                if (maxPrice.HasValue)
                {
                    query = query.Where(x => x.PriceIdr <= maxPrice.Value);
                }

                query = sortBy switch
                {
                    "price_asc" => query.OrderBy(x => x.PriceIdr),
                    "price_desc" => query.OrderByDescending(x => x.PriceIdr),
                    "best_seller" => query.OrderByDescending(x => x.SoldCount),
                    "rating" => query.OrderByDescending(x => x.RatingAvg),
                    _ => query.OrderByDescending(x => x.UpdatedAt)
                };

                return await query.Select(x => new GetProductResponse
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
                    Seller = x.Seller == null ? string.Empty : x.Seller.ShopName,
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetProductResponse>();
            }
        }

        public async Task<GetProductDetailResponse> GetProductDetail(Guid productId)
        {
            try
            {
                var product = await _context.TrProducts
                    .Include(x => x.Category)
                    .Include(x => x.Seller)
                    .FirstOrDefaultAsync(x => x.Id == productId);

                if (product == null)
                {
                    return new GetProductDetailResponse
                    {
                        Status = false,
                        Message = "Product not found"
                    };
                }

                var specs = await _context.LtProductSpecifications
                    .Where(x => x.ProductId == productId)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new GetProductSpecResponse
                    {
                        Key = x.SpecKey,
                        Value = x.SpecValue
                    }).ToListAsync();

                return new GetProductDetailResponse
                {
                    Status = true,
                    Message = "Success",
                    product = new GetProductResponse
                    {
                        Id = product.Id,
                        Name = product.Name,
                        Category = product.Category == null ? string.Empty : product.Category.Name,
                        Price = product.PriceIdr,
                        Sold = product.SoldCount,
                        Stock = product.Stock,
                        Image = product.Image,
                        RatingAvg = product.RatingAvg,
                        Description = product.Description,
                        Seller = product.Seller == null ? string.Empty : product.Seller.ShopName,
                    },
                    sellerId = product.SellerId,
                    sellerCity = product.Seller?.City,
                    sellerRating = product.Seller == null ? 0 : product.Seller.RatingAvg,
                    ratingCount = product.RatingCount,
                    specifications = specs
                };
            }
            catch (Exception ex)
            {
                return new GetProductDetailResponse
                {
                    Status = false,
                    Message = $"Server error occurred while getting product"
                };
            }
        }

        public async Task<List<GetProductResponse>> GetProductsBySeller(Guid sellerId)
        {
            try
            {
                return await _context.TrProducts
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
                        Seller = x.Seller == null ? string.Empty : x.Seller.ShopName,
                    }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetProductResponse>();
            }
        }

        public async Task<ResultMessageResponse> UpdateProduct(Guid sellerId, Guid productId, FormItemsRequest req)
        {
            try
            {
                if (req.Stock < 0)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Stock cannot be negative"
                    };
                }

                if (req.PriceIdr <= 0)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Price must be greater than 0"
                    };
                }

                var product = await _context.TrProducts
                    .Include(x => x.Seller)
                    .FirstOrDefaultAsync(x => x.Id == productId && x.SellerId == sellerId);

                if (product == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Product not found"
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

                product.Name = req.Name;
                product.CategoryId = req.CategoryId;
                product.PriceIdr = req.PriceIdr;
                product.Stock = req.Stock;
                product.Description = req.Description;
                product.Image = string.IsNullOrWhiteSpace(req.Image) ? product.Image : req.Image;
                product.UpdatedAt = DateTime.UtcNow;
                product.UpdatedBy = product.Seller!.UserId;

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Product updated successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while updating product"
                };
            }
        }
    }
}
