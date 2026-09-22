using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    public class CartService
    {
        private readonly ApplicationDBContext _context;

        public CartService(ApplicationDBContext context)
        {
            _context = context;
        }

        private async Task<bool> FindUserById(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return false;
            }

            return await _context.MsUsers.AnyAsync(x => x.Id == userId);
        }

        public async Task<List<GetCartResponse>> GetAllCarts()
        {
            try
            {
                return await _context.TrCartItems.Select(x => new GetCartResponse
                {
                    Id = x.Id,
                    userId = x.UserId,
                    productId = x.ProductId,
                    qty = x.Quantity,
                    price = x.Product == null ? 0 : x.Product.PriceIdr,
                    slug = x.Product == null ? "" : x.Product.Slug
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetCartResponse>();
            }
        }

        public async Task<ResultMessageResponse> UpsertToCart(Guid userId, Guid productId)
        {
            try
            {
                if (!await FindUserById(userId))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "User not found"
                    };
                }

                var stockAvailable = await _context.TrProducts.Where(x => x.Id == productId).FirstOrDefaultAsync();
                if (stockAvailable != null)
                {
                    if (stockAvailable.Stock < 1)
                    {
                        return new ResultMessageResponse
                        {
                            Status = false,
                            Message = "Insufficient stock"
                        };
                    }
                }
                else
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Item not found"
                    };
                }

                var isAlreadyInCart = await _context.TrCartItems.FirstOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId && x.Quantity > 0);
                if (isAlreadyInCart != null)
                {
                    isAlreadyInCart.Quantity += 1;
                    isAlreadyInCart.UpdatedAt = DateTime.UtcNow;
                    isAlreadyInCart.IsSelected = true;
                }
                else
                {
                    var newCartItem = new TrCartItems
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        ProductId = productId,
                        Quantity = 1,
                        IsSelected = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.TrCartItems.Add(newCartItem);
                }

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Item added to cart successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while adding item to cart"
                };
            }
        }
    }
}
