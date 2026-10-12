using chainshop_b.Data;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    public class CourierService
    {
        private readonly ApplicationDBContext _context;

        public CourierService(ApplicationDBContext context)
        {
            _context = context;
        }

        public async Task<List<GetCourierResponse>> GetAllCouriers()
        {
            try
            {
                return await _context.MsCourierCompanies
                    .OrderBy(x => x.Name)
                    .Select(x => new GetCourierResponse
                    {
                        Id = x.Id,
                        Name = x.Name,
                        activeDrivers = _context.TrDrivers.Count(d => d.CourierCompanyId == x.Id && d.IsActive)
                    }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetCourierResponse>();
            }
        }

        public async Task<List<GetDriverResponse>> GetDrivers(Guid? courierCompanyId)
        {
            try
            {
                var query = _context.TrDrivers.Where(x => x.IsActive);
                if (courierCompanyId.HasValue && courierCompanyId.Value != Guid.Empty)
                {
                    query = query.Where(x => x.CourierCompanyId == courierCompanyId.Value);
                }

                return await query
                    .OrderBy(x => x.FullName)
                    .Select(x => new GetDriverResponse
                    {
                        Id = x.Id,
                        fullName = x.FullName,
                        phone = x.Phone,
                        vehicleType = x.VehicleType,
                        plateNumber = x.PlateNumber,
                        courierCompanyId = x.CourierCompanyId,
                        courierCompany = x.CourierCompany == null ? "" : x.CourierCompany.Name
                    }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetDriverResponse>();
            }
        }
    }
}
