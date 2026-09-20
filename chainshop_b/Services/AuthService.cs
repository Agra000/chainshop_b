using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    public class AuthService
    {
        private readonly ApplicationDBContext _context;
        private readonly IConfiguration _config;

        public AuthService(ApplicationDBContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        private async Task<MsUsers?> FindUserEmail(string email)
        {
            return await _context.MsUsers.FirstOrDefaultAsync(x => x.Email == email);
        }

        //private string GenerateJwtToken(MsUsers user)
        //{
        //    var claims = new List<Claim>
        //    {
        //        new Claim("iduser", user.Id.ToString()),
        //        new Claim(ClaimTypes.Email, user.Email),
        //        new Claim("fullname", user.FullName != null ? user.FullName.Split(' ')[0] : "user"),
        //        new Claim("role", user.Role ?? "user"),
        //        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        //    };

        //    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        //    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        //    var token = new JwtSecurityToken(
        //        issuer: _config["Jwt:Issuer"],
        //        audience: _config["Jwt:Audience"],
        //        claims: claims,
        //        expires: DateTime.Now.AddHours(3),
        //        signingCredentials: creds
        //    );

        //    return new JwtSecurityTokenHandler().WriteToken(token);
        //}

        public async Task<ResultMessageResponse> Register(RegisterRequest req)
        {
            try
            {
                var newUserEmail = await FindUserEmail(req.Email);
                if (newUserEmail != null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "User with this email already exists"
                    };
                }

                //var pwLen = req.Password.Length;
                //if (pwLen < 6)
                //{
                //    return new ResultMessageResponse
                //    {
                //        Status = false,
                //        Message = "Password must be at least 6 characters long"
                //    };
                //}

                //if (req.Password != req.ConfirmPassword)
                //{
                //    return new ResultMessageResponse
                //    {
                //        Status = false,
                //        Message = "Passwords do not match"
                //    };
                //}

                var newUserGuid = Guid.NewGuid();
                var newData = new MsUsers
                {
                    Id = newUserGuid,
                    Email = req.Email,
                    Name = req.Name == null ? "user" : req.Name,
                    Phone = req.Phone,
                    City = req.City,
                    AuthMethod = "Email",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                };

                _context.MsUsers.Add(newData);
                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "User registered successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while registering user"
                };
            }
        }

        public async Task<ResultMessageResponse> WalletLogin(string walletID)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(walletID))
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Wallet address tidak boleh kosong."
                    };
                }

                // Normalkan address ke lowercase agar tidak ada isu perbedaan huruf kapital
                string formattedAddress = walletID.ToLower();

                // Cek apakah wallet address sudah ada di database
                var user = await _context.MsUsers
                    .FirstOrDefaultAsync(u => u.WalletAddress != null && u.WalletAddress.ToLower() == formattedAddress);

                // Jika belum terdaftar, buat data user baru
                if (user == null)
                {
                    user = new MsUsers
                    {
                        Id = Guid.NewGuid(),
                        WalletAddress = formattedAddress,
                        AuthMethod = "wallet", // Sesuai check constraint di schema
                        Name = "ChainShop User", // Default name dari schema[cite: 1]
                        CreatedAt = DateTime.UtcNow,
                        LastLoginAt = DateTime.UtcNow
                    };

                    _context.MsUsers.Add(user);
                }
                else
                {
                    // Jika sudah terdaftar, perbarui waktu login terakhir
                    user.LastLoginAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Berhasil otentikasi wallet",
                    idToken = user.Name,
                    walletAddress = user.WalletAddress
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while processing wallet login"
                };
            }
        }

        public async Task<ResultMessageResponse> Login(LoginRequest req)
        {
            try
            {
                //var googleResult = await checkReCaptcha(r.tokenCaptcha);
                //if (googleResult == null || !googleResult.Success)
                //{
                //    return new ResultMessageResponse
                //    {
                //        Status = false,
                //        Message = "Captcha is not valid or already expired !."
                //    };
                //}

                var user = await FindUserEmail(req.email);
                if (user == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Email is not registered yet !!"
                    };
                }

                //bool isPwValid = BCrypt.Net.BCrypt.Verify(req.password, user.Password);
                //if (!isPwValid)
                //{
                //    return new ResultMessageResponse
                //    {
                //        Status = false,
                //        Message = "Wrong Password !!"
                //    };
                //}

                //var tokenString = GenerateJwtToken(user.user_id, user.email, user.username);
                //var cookieOptions = new CookieOptions
                //{
                //    HttpOnly = true,
                //    Secure = false,
                //    SameSite = SameSiteMode.Lax,
                //    Path = "/",
                //    Expires = DateTime.Now.AddDays(1)
                //};
                //Response.Cookies.Append("token", tokenString, cookieOptions);

                //var token = GenerateJwtToken(user);

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Login Successful",
                    //idToken = token
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server Error. Please Try Again !"
                };
            }
        }
    }
}
