using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
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
                    image = x.Product == null ? "" : x.Product.Image
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
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = userId
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

        public async Task<List<GetCartResponse>> GetCartByUser(Guid userId)
        {
            try
            {
                return await _context.TrCartItems
                    .Where(x => x.UserId == userId && x.Quantity > 0)
                    .OrderByDescending(x => x.UpdatedAt)
                    .Select(x => new GetCartResponse
                    {
                        Id = x.Id,
                        userId = x.UserId,
                        productId = x.ProductId,
                        qty = x.Quantity,
                        price = x.Product == null ? 0 : x.Product.PriceIdr,
                        image = x.Product == null ? "" : x.Product.Image,
                        productName = x.Product == null ? "" : x.Product.Name,
                        stock = x.Product == null ? 0 : x.Product.Stock,
                        isSelected = x.IsSelected,
                        lineTotal = x.Product == null ? 0 : x.Product.PriceIdr * x.Quantity,
                        sellerId = x.Product == null ? null : x.Product.SellerId,
                        shopName = x.Product == null || x.Product.Seller == null ? "" : x.Product.Seller.ShopName
                    }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetCartResponse>();
            }
        }

        public async Task<ResultMessageResponse> UpdateCartItem(Guid userId, UpdateCartRequest req)
        {
            try
            {
                var cartItem = await _context.TrCartItems
                    .Include(x => x.Product)
                    .FirstOrDefaultAsync(x => x.Id == req.CartId && x.UserId == userId);

                if (cartItem == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Cart item not found"
                    };
                }

                // quantity 0 atau kurang = hapus item dari cart
                if (req.Quantity < 1)
                {
                    _context.TrCartItems.Remove(cartItem);
                    await _context.SaveChangesAsync();

                    return new ResultMessageResponse
                    {
                        Status = true,
                        Message = "Item removed from cart"
                    };
                }

                if (cartItem.Product == null || cartItem.Product.Stock < req.Quantity)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Insufficient stock"
                    };
                }

                cartItem.Quantity = req.Quantity;
                if (req.IsSelected.HasValue)
                {
                    cartItem.IsSelected = req.IsSelected.Value;
                }
                cartItem.UpdatedAt = DateTime.UtcNow;
                cartItem.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Cart updated successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while updating cart"
                };
            }
        }

        public async Task<ResultMessageResponse> SelectCartItem(Guid userId, Guid cartId, bool isSelected)
        {
            try
            {
                var cartItem = await _context.TrCartItems.FirstOrDefaultAsync(x => x.Id == cartId && x.UserId == userId);
                if (cartItem == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Cart item not found"
                    };
                }

                cartItem.IsSelected = isSelected;
                cartItem.UpdatedAt = DateTime.UtcNow;
                cartItem.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = isSelected ? "Item selected" : "Item unselected"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while updating cart"
                };
            }
        }

        public async Task<ResultMessageResponse> RemoveFromCart(Guid userId, Guid cartId)
        {
            try
            {
                var cartItem = await _context.TrCartItems.FirstOrDefaultAsync(x => x.Id == cartId && x.UserId == userId);
                if (cartItem == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Cart item not found"
                    };
                }

                _context.TrCartItems.Remove(cartItem);
                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Item removed from cart"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while removing item from cart"
                };
            }
        }
    }
}
