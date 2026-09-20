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
                    ShopSlug = req.ShopSlug,
                    ShopDescription = req.ShopDescription ?? string.Empty,
                    City = req.City ?? string.Empty,
                    PayoutWalletAddress = req.PayoutWalletAddress,
                    IsVerified = false,
                    RatingAvg = 0,
                    RatingCount = 0,
                    CreatedAt = DateTime.UtcNow,
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
    }
}
