//using Microsoft.EntityFrameworkCore;
//using chainshop_b.Data;
//using chainshop_b.Model;
//using chainshop_b.Model.Dto.Response;

//namespace chainshop_b.Services
//{
//    public class CartService
//    {
//        private readonly ApplicationDBContext _context;

//        public CartService(ApplicationDBContext context)
//        {
//            _context = context;
//        }

//        private async Task<bool> FindUserById(Guid userId)
//        {
//            if (userId == Guid.Empty)
//            {
//                return false;
//            }

//            return await _context.MsUsers.AnyAsync(x => x.Id == userId);
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

//        public async Task<ResultMessageResponse> UpsertToCart(Guid userId, Guid trItemId)
//        {
//            try
//            {
//                if (!await FindUserById(userId))
//                {
//                    return new ResultMessageResponse
//                    {
//                        Status = false,
//                        Message = "User not found"
//                    };
//                }

//                var stockAvailable = await _context.TrItem.Where(x => x.Id == trItemId).FirstOrDefaultAsync();
//                if (stockAvailable != null)
//                {
//                    if (stockAvailable.StokQty < 1)
//                    {
//                        return new ResultMessageResponse
//                        {
//                            Status = false,
//                            Message = "Insufficient stock available"
//                        };
//                    }
//                }
//                else
//                {
//                    return new ResultMessageResponse
//                    {
//                        Status = false,
//                        Message = "Item not found"
//                    };
//                }

//                Guid cartId = await _context.TrCart.Where(x => x.UserId == userId).Select(x => x.Id).FirstOrDefaultAsync();
//                if (cartId == Guid.Empty)
//                {
//                    Guid newCartId = Guid.NewGuid();

//                    var newCart = new TrCart
//                    {
//                        DateIn = DateTime.Now,
//                        UserIn = userId,
//                        Id = newCartId,
//                        UserId = userId
//                    };
//                    _context.TrCart.Add(newCart);

//                    var newCartItem = new TrCartItems
//                    {
//                        DateIn = DateTime.Now,
//                        UserIn = userId,
//                        Id = Guid.NewGuid(),
//                        CartId = newCartId,
//                        TrItemId = trItemId,
//                        Quantity = 1,
//                        PriceAtAdd = stockAvailable.Price
//                    };
//                    _context.TrCartItem.Add(newCartItem);
//                }
//                else
//                {
//                    var isAlreadyInCart = await _context.TrCartItem.FirstOrDefaultAsync(x => x.CartId == cartId && x.TrItemId == trItemId && x.Quantity > 0);

//                    if (isAlreadyInCart != null)
//                    {
//                        isAlreadyInCart.Quantity += 1;
//                        isAlreadyInCart.DateUp = DateTime.Now;
//                        isAlreadyInCart.UserUp = userId;
//                        isAlreadyInCart.PriceAtAdd = stockAvailable.Price * isAlreadyInCart.Quantity;
//                    }
//                    else
//                    {
//                        var newCartItem = new TrCartItems
//                        {
//                            DateIn = DateTime.Now,
//                            UserIn = userId,
//                            Id = Guid.NewGuid(),
//                            CartId = cartId,
//                            TrItemId = trItemId,
//                            Quantity = 1,
//                            PriceAtAdd = stockAvailable.Price
//                        };
//                        _context.TrCartItem.Add(newCartItem);
//                    }
//                }
//                await _context.SaveChangesAsync();

//                return new ResultMessageResponse
//                {
//                    Status = true,
//                    Message = "Item added to cart successfully"
//                };
//            }
//            catch (Exception ex)
//            {
//                return new ResultMessageResponse
//                {
//                    Status = false,
//                    Message = $"Server error occurred while adding item to cart"
//                };
//            }
//        }
//    }
//}
