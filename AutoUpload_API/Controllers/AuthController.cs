using AutoUpload_API.Data;
using AutoUpload_API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace AutoUpload_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;

        public AuthController(IConfiguration config, AppDbContext db)
        {
            _config = config;
            _db = db;
        }

        [HttpGet("google/login")]
        public IActionResult GoogleLogin()
        {
            var clientId = _config["GoogleOAuth:ClientId"];
            var redirectUri = _config["GoogleOAuth:RedirectUri"];
            var scope = "openid email profile";

            var googleAuthUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
                              $"?client_id={clientId}" +
                              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                              $"&scope={Uri.EscapeDataString(scope)}" +
                              $"&response_type=code" +
                              $"&access_type=offline" +
                              $"&prompt=consent";

            return Ok(new { authUrl = googleAuthUrl });
        }

        [HttpPost("google/callback")]
        public async Task<IActionResult> GoogleCallback([FromBody] GoogleCallbackRequest request)
        {
            try
            {
                // 1. Đổi code lấy token
                using var client = new HttpClient();
                var values = new Dictionary<string, string>
                {
                    { "code", request.Code },
                    { "client_id", _config["GoogleOAuth:ClientId"] },
                    { "client_secret", _config["GoogleOAuth:ClientSecret"] },
                    { "redirect_uri", _config["GoogleOAuth:RedirectUri"] },
                    { "grant_type", "authorization_code" }
                };

                var response = await client.PostAsync("https://oauth2.googleapis.com/token",
                    new FormUrlEncodedContent(values));

                if (!response.IsSuccessStatusCode)
                {
                    return BadRequest("Failed to exchange code for token");
                }

                var json = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // 2. Lấy thông tin user từ Google
                var userInfo = await GetGoogleUserInfo(tokenResponse.AccessToken);

                // 3. Tìm hoặc tạo user trong DB
                var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == userInfo.Id);
                if (user == null)
                {
                    user = new User
                    {
                        GoogleId = userInfo.Id,
                        Email = userInfo.Email,
                        Name = userInfo.Name,
                        AvatarUrl = userInfo.Picture,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Users.Add(user);
                }

                // 4. Cập nhật token và thông tin đăng nhập
                user.GoogleAccessToken = tokenResponse.AccessToken;
                user.GoogleRefreshToken = tokenResponse.RefreshToken ?? user.GoogleRefreshToken;
                user.AccessTokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                user.LastLoginAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                // 5. Tạo JWT token cho client
                var jwtToken = GenerateJwtToken(user);

                return Ok(new LoginResponse
                {
                    Token = jwtToken,
                    User = new UserInfo
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = user.Name,
                        AvatarUrl = user.AvatarUrl
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Authentication failed: {ex.Message}");
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var principal = GetPrincipalFromExpiredToken(request.Token);
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                    return Unauthorized("Invalid token");

                var user = await _db.Users.FindAsync(Guid.Parse(userId));
                if (user == null)
                    return Unauthorized("User not found");

                var newJwtToken = GenerateJwtToken(user);
                return Ok(new { token = newJwtToken });
            }
            catch
            {
                return Unauthorized("Invalid token");
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // Có thể thêm logic revoke Google token nếu cần
            return Ok(new { message = "Logged out successfully" });
        }

        private async Task<GoogleUserInfo> GetGoogleUserInfo(string accessToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to fetch user info from Google");
            }
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GoogleUserInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_config["Jwt:Secret"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim("GoogleId", user.GoogleId)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_config["Jwt:Secret"])),
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }

        // DTO classes
        public class GoogleCallbackRequest
        {
            public string Code { get; set; }
        }

        public class RefreshTokenRequest
        {
            public string Token { get; set; }
        }

        public class LoginResponse
        {
            public string Token { get; set; }
            public UserInfo User { get; set; }
        }

        public class UserInfo
        {
            public Guid Id { get; set; }
            public string Email { get; set; }
            public string Name { get; set; }
            public string AvatarUrl { get; set; }
        }

        public class GoogleUserInfo
        {
            public string Id { get; set; }
            public string Email { get; set; }
            public string Name { get; set; }
            public string Picture { get; set; }
        }

        public class GoogleTokenResponse
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
            public int ExpiresIn { get; set; }
            public string TokenType { get; set; }
        }
    }
}