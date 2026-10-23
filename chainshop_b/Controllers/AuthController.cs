using chainshop_b.Model.Dto.Request;
using chainshop_b.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [AllowAnonymous]
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

        [AllowAnonymous]
        [HttpPost("wallet-login")]
        public async Task<IActionResult> WalletLogin([FromBody] string walletID)
        {
            var res = await _authService.WalletLogin(walletID);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                userId = res.userId,
                token = res.token,
                expiresAt = res.expiresAt
            });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var res = await _authService.Login(req);

            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                idToken = res.userId,
                userId = res.userId,
                token = res.token,
                expiresAt = res.expiresAt
            });
        }

        [HttpGet("get-user")]
        public async Task<IActionResult> GetUserInfo([FromQuery] Guid userId)
        {
            var res = await _authService.GetUserInfo(userId);
            return Ok(new
            {
                status = res.Status,
                message = res.Message,
                username = res.username,
                storeId = res.storeId,
                storeName = res.storeName,
                email = res.email,
                walletAddress = res.walletAddress,
                phone = res.phone,
                city = res.city,
                authMethod = res.authMethod,
                lastLoginAt = res.lastLoginAt,
                joinedAt = res.joinedAt
            });
        }

        [HttpPut("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromQuery] Guid userId, [FromBody] RegisterRequest req)
        {
            var res = await _authService.UpdateProfile(userId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }
    }
}
