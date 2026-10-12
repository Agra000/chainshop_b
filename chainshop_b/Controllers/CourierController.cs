using chainshop_b.Services;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class CourierController : ControllerBase
    {
        private readonly CourierService _courierService;

        public CourierController(CourierService courierService)
        {
            _courierService = courierService;
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllCouriers()
        {
            var res = await _courierService.GetAllCouriers();
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpGet("get-drivers")]
        public async Task<IActionResult> GetDrivers([FromQuery] Guid? courierCompanyId)
        {
            var res = await _courierService.GetDrivers(courierCompanyId);
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }
    }
}
