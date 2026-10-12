using chainshop_b.Model.Dto.Request;
using chainshop_b.Services;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class AddressController : ControllerBase
    {
        private readonly AddressService _addressService;

        public AddressController(AddressService addressService)
        {
            _addressService = addressService;
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAddresses([FromQuery] Guid userId)
        {
            var res = await _addressService.GetAddresses(userId);
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpPost("upsert")]
        public async Task<IActionResult> UpsertAddress([FromQuery] Guid userId, [FromBody] AddressRequest req)
        {
            var res = await _addressService.UpsertAddress(userId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpPut("set-default")]
        public async Task<IActionResult> SetDefaultAddress([FromQuery] Guid userId, [FromQuery] Guid addressId)
        {
            var res = await _addressService.SetDefaultAddress(userId, addressId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteAddress([FromQuery] Guid userId, [FromQuery] Guid addressId)
        {
            var res = await _addressService.DeleteAddress(userId, addressId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }
    }
}
