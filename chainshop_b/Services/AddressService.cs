using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    public class AddressService
    {
        private readonly ApplicationDBContext _context;

        public AddressService(ApplicationDBContext context)
        {
            _context = context;
        }

        private async Task ResetDefaultAddress(Guid userId, Guid exceptId)
        {
            var defaults = await _context.MsAddresses
                .Where(x => x.UserId == userId && x.IsDefault && x.Id != exceptId)
                .ToListAsync();

            foreach (var item in defaults)
            {
                item.IsDefault = false;
                item.UpdatedAt = DateTime.UtcNow;
                item.UpdatedBy = userId;
            }
        }

        public async Task<List<GetAddressResponse>> GetAddresses(Guid userId)
        {
            try
            {
                return await _context.MsAddresses
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.IsDefault)
                    .ThenByDescending(x => x.UpdatedAt)
                    .Select(x => new GetAddressResponse
                    {
                        Id = x.Id,
                        Label = x.Label,
                        RecipientName = x.RecipientName,
                        Phone = x.Phone,
                        FullAddress = x.FullAddress,
                        City = x.City,
                        Province = x.Province,
                        PostalCode = x.PostalCode,
                        IsDefault = x.IsDefault
                    }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetAddressResponse>();
            }
        }

        public async Task<ResultMessageResponse> UpsertAddress(Guid userId, AddressRequest req)
        {
            try
            {
                var user = await _context.MsUsers.AnyAsync(x => x.Id == userId);
                if (!user)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "User not found"
                    };
                }

                if (string.IsNullOrWhiteSpace(req.RecipientName) || string.IsNullOrWhiteSpace(req.Phone) ||
                    string.IsNullOrWhiteSpace(req.FullAddress) || string.IsNullOrWhiteSpace(req.City) ||
                    string.IsNullOrWhiteSpace(req.Province))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Please complete the address form"
                    };
                }

                // alamat pertama otomatis jadi default
                var hasAddress = await _context.MsAddresses.AnyAsync(x => x.UserId == userId);
                var isDefault = req.IsDefault || !hasAddress;

                MsAddresses? address = null;
                if (req.Id.HasValue && req.Id.Value != Guid.Empty)
                {
                    address = await _context.MsAddresses.FirstOrDefaultAsync(x => x.Id == req.Id.Value && x.UserId == userId);
                    if (address == null)
                    {
                        return new ResultMessageResponse
                        {
                            Status = false,
                            Message = "Address not found"
                        };
                    }

                    address.Label = req.Label;
                    address.RecipientName = req.RecipientName;
                    address.Phone = req.Phone;
                    address.FullAddress = req.FullAddress;
                    address.City = req.City;
                    address.Province = req.Province;
                    address.PostalCode = req.PostalCode;
                    address.IsDefault = isDefault || address.IsDefault;
                    address.UpdatedAt = DateTime.UtcNow;
                    address.UpdatedBy = userId;
                }
                else
                {
                    address = new MsAddresses
                    {
                        UserId = userId,
                        Label = string.IsNullOrWhiteSpace(req.Label) ? "Home" : req.Label,
                        RecipientName = req.RecipientName,
                        Phone = req.Phone,
                        FullAddress = req.FullAddress,
                        City = req.City,
                        Province = req.Province,
                        PostalCode = req.PostalCode,
                        IsDefault = isDefault,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = userId
                    };
                    _context.MsAddresses.Add(address);
                }

                if (address.IsDefault)
                {
                    await ResetDefaultAddress(userId, address.Id);
                }

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Address saved successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while saving address"
                };
            }
        }

        public async Task<ResultMessageResponse> SetDefaultAddress(Guid userId, Guid addressId)
        {
            try
            {
                var address = await _context.MsAddresses.FirstOrDefaultAsync(x => x.Id == addressId && x.UserId == userId);
                if (address == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Address not found"
                    };
                }

                address.IsDefault = true;
                address.UpdatedAt = DateTime.UtcNow;
                address.UpdatedBy = userId;
                await ResetDefaultAddress(userId, address.Id);

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Default address updated"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while updating address"
                };
            }
        }

        public async Task<ResultMessageResponse> DeleteAddress(Guid userId, Guid addressId)
        {
            try
            {
                var address = await _context.MsAddresses.FirstOrDefaultAsync(x => x.Id == addressId && x.UserId == userId);
                if (address == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Address not found"
                    };
                }

                var usedInOrder = await _context.TrOrders.AnyAsync(x => x.AddressId == addressId);
                if (usedInOrder)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Address is already used in an order and cannot be deleted"
                    };
                }

                _context.MsAddresses.Remove(address);
                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Address deleted successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while deleting address"
                };
            }
        }
    }
}
