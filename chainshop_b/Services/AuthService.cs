using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

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

        private (string token, DateTime expiresAt) GenerateJwtToken(MsUsers user)
        {
            var claims = new List<Claim>
            {
                new Claim("iduser", user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("fullname", user.Name),
                new Claim("auth_method", user.AuthMethod),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
            }

            if (!string.IsNullOrWhiteSpace(user.WalletAddress))
            {
                claims.Add(new Claim("wallet", user.WalletAddress));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expireHours = int.TryParse(_config["Jwt:ExpireHours"], out var hours) ? hours : 24;
            var expiresAt = DateTime.UtcNow.AddHours(expireHours);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }

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
                    UpdatedAt = DateTime.UtcNow,
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

        public async Task<JwtKwResponse> WalletLogin(string walletID)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(walletID))
                {
                    return new JwtKwResponse
                    {
                        Status = false,
                        Message = "Wallet address tidak boleh kosong."
                    };
                }

                // Normalkan address ke lowercase agar tidak ada isu perbedaan huruf kapital
                string formattedAddress = walletID.ToLower();

                var user = await _context.MsUsers
                    .FirstOrDefaultAsync(u => u.WalletAddress != null && u.WalletAddress.ToLower() == formattedAddress);

                if (user == null)
                {
                    user = new MsUsers
                    {
                        Id = Guid.NewGuid(),
                        WalletAddress = formattedAddress,
                        AuthMethod = "wallet",
                        Name = "User",
                        UpdatedAt = DateTime.UtcNow,
                        LastLoginAt = DateTime.UtcNow
                    };

                    _context.MsUsers.Add(user);
                }
                else
                {
                    user.LastLoginAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                var jwt = GenerateJwtToken(user);

                return new JwtKwResponse
                {
                    Status = true,
                    Message = "Berhasil otentikasi wallet",
                    userId = user.Id,
                    walletAddress = user.WalletAddress,
                    token = jwt.token,
                    expiresAt = jwt.expiresAt.ToString("o")
                };
            }
            catch (Exception ex)
            {
                return new JwtKwResponse
                {
                    Status = false,
                    Message = $"Server error occurred while processing wallet login"
                };
            }
        }

        public async Task<JwtKwResponse> Login(LoginRequest req)
        {
            try
            {
                //var googleResult = await checkReCaptcha(r.tokenCaptcha);
                //if (googleResult == null || !googleResult.Success)
                //{
                //    return new JwtKwResponse
                //    {
                //        Status = false,
                //        Message = "Captcha is not valid or already expired !."
                //    };
                //}

                var user = await FindUserEmail(req.email);
                if (user == null)
                {
                    return new JwtKwResponse
                    {
                        Status = false,
                        Message = "Email is not registered yet !!"
                    };
                }

                //bool isPwValid = BCrypt.Net.BCrypt.Verify(req.password, user.Password);
                //if (!isPwValid)
                //{
                //    return new JwtKwResponse
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

                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var jwt = GenerateJwtToken(user);

                return new JwtKwResponse
                {
                    Status = true,
                    Message = "Login Successful",
                    userId = user.Id,
                    email = user.Email,
                    walletAddress = user.WalletAddress,
                    token = jwt.token,
                    expiresAt = jwt.expiresAt.ToString("o")
                };
            }
            catch (Exception ex)
            {
                return new JwtKwResponse
                {
                    Status = false,
                    Message = $"Server Error. Please Try Again !"
                };
            }
        }

        public async Task<JwtKwResponse> GetUserInfo(Guid userId)
        {
            try
            {
                if (userId == Guid.Empty)
                {
                    return new JwtKwResponse
                    {
                        Status = false,
                        Message = "User Id is Empty"
                    };
                }

                var user = await _context.MsUsers
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return new JwtKwResponse
                    {
                        Status = false,
                        Message = "User Not Found"
                    };
                }

                var store = await _context.MsSellers
                    .Where(x => x.UserId == user.Id)
                    .FirstOrDefaultAsync();

                return new JwtKwResponse
                {
                    Status = true,
                    Message = "User Data Collected !",
                    username = user.Name,
                    storeId = store == null ? "" : store.Id.ToString(),
                    storeName = store == null ? "" : store.ShopName,
                    email = user.Email == null ? "" : user.Email,
                    walletAddress = user.WalletAddress,
                    phone = user.Phone,
                    city = user.City,
                    authMethod = user.AuthMethod,
                    lastLoginAt = user.LastLoginAt == null ? null : user.LastLoginAt.Value.ToString("o")
                };
            }
            catch (Exception ex)
            {
                return new JwtKwResponse
                {
                    Status = false,
                    Message = $"Server error occurred while processing wallet login"
                };
            }
        }
    
        public async Task<ResultMessageResponse> UpdateProfile(Guid userId, RegisterRequest req)
        {
            try
            {
                var user = await _context.MsUsers.FirstOrDefaultAsync(x => x.Id == userId);
                if (user == null)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "User Not Found"
                    };
                }

                if (!string.IsNullOrWhiteSpace(req.Email) && req.Email != user.Email)
                {
                    var emailUsed = await _context.MsUsers.AnyAsync(x => x.Email == req.Email && x.Id != userId);
                    if (emailUsed)
                    {
                        return new ResultMessageResponse
                        {
                            Status = false,
                            Message = "Email is already used by another account"
                        };
                    }
                    user.Email = req.Email;
                }

                user.Name = string.IsNullOrWhiteSpace(req.Name) ? user.Name : req.Name.Trim();
                user.Phone = req.Phone;
                user.City = req.City;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Profile updated successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while updating profile"
                };
            }
        }
    }
}
