using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using chainshop_b.Services;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var res = await _authService.Register(req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        [HttpPost("wallet-login")]
        public async Task<IActionResult> WalletLogin([FromBody] string walletID)
        {
            var res = await _authService.WalletLogin(walletID);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                idToken = res.idToken,
                walletAddress = res.walletAddress
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var res = await _authService.Login(req);

            return Ok(new ResultMessageResponse
            {
                Status = res.Status,
                Message = res.Message,
                idToken = res.idToken
            });
        }
    }
}
