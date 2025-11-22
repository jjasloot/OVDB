using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AuthenticationController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly PasswordHasher<User> passwordHasher;
        
        // Token expiration settings
        private const int RefreshTokenExpirationDays = 30;
        private const int RevokedTokenRetentionDays = 7;
        private const int MaxDeviceInfoLength = 500;
        private const string DeviceInfoTruncationSuffix = "...";

        public AuthenticationController(IConfiguration configuration, OVDBDatabaseContext dbContext)
        {
            _configuration = configuration;
            _dbContext = dbContext;
            passwordHasher = new PasswordHasher<User>();
        }


        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<ActionResult<LoginResponse>> LoginAsync([FromBody] LoginRequest loginRequest)
        {

            if (string.IsNullOrWhiteSpace(loginRequest.Email) || string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                return Unauthorized();
            }

            return await LogUserIn(loginRequest);
        }

        private async Task<ActionResult<LoginResponse>> LogUserIn(LoginRequest loginRequest)
        {
            var user = await _dbContext.Users
                .SingleOrDefaultAsync(u => u.Email == loginRequest.Email.ToLower());
            var passwordCorrect = false;

            if (user != null)
            {
                var result = passwordHasher.VerifyHashedPassword(user, user.Password, loginRequest.Password);
                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    user.Password = passwordHasher.HashPassword(user, loginRequest.Password);
                }
                if (result == PasswordVerificationResult.SuccessRehashNeeded || result == PasswordVerificationResult.Success)
                {
                    passwordCorrect = true;
                }
            }

            if (passwordCorrect == false)
            {
                return Forbid();
            }
            
            var claims = new List<Claim>
            {
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email),
                new Claim("admin",user.IsAdmin?"true":"false")
            };

            var accessToken = GenerateAccessToken(claims);
            var refreshToken = await GenerateRefreshToken(user.Id);

            user.LastLogin = DateTime.UtcNow;
            _dbContext.Update(user);
            await _dbContext.SaveChangesAsync();

            return new LoginResponse { Token = accessToken, RefreshToken = refreshToken.Token };
        }

        [HttpPost("refreshToken")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> RefreshTokenAsync([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest("Refresh token is required");
            }

            // Find the refresh token in the database
            var refreshToken = await _dbContext.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

            if (refreshToken == null)
            {
                return Unauthorized("Invalid refresh token");
            }

            // Validate the refresh token
            if (refreshToken.IsRevoked)
            {
                return Unauthorized("Refresh token has been revoked");
            }

            if (refreshToken.IsExpired)
            {
                return Unauthorized("Refresh token has expired");
            }

            var user = refreshToken.User;

            // Generate new access token
            var claims = new List<Claim>
            {
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email),
                new Claim("admin", user.IsAdmin ? "true" : "false")
            };

            var newAccessToken = GenerateAccessToken(claims);

            // Token rotation: Generate a new refresh token and revoke the old one
            var newRefreshToken = await GenerateRefreshToken(user.Id, refreshToken.DeviceInfo);
            
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            
            // Update last used timestamp
            refreshToken.LastUsedAt = DateTime.UtcNow;
            user.LastLogin = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return new LoginResponse
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken.Token
            };
        }

        [HttpPost("logout")]
        public async Task<IActionResult> LogoutAsync([FromBody] RefreshTokenRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                var refreshToken = await _dbContext.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

                if (refreshToken != null && !refreshToken.IsRevoked)
                {
                    refreshToken.IsRevoked = true;
                    refreshToken.RevokedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
            }

            return Ok();
        }

        [HttpGet("sessions")]
        public async Task<ActionResult<IEnumerable<object>>> GetActiveSessionsAsync()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized();
            }

            var activeSessions = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId.Value && !rt.IsRevoked && !rt.IsExpired)
                .OrderByDescending(rt => rt.LastUsedAt ?? rt.CreatedAt)
                .Select(rt => new
                {
                    rt.Id,
                    rt.DeviceInfo,
                    rt.CreatedAt,
                    rt.LastUsedAt,
                    rt.ExpiresAt
                })
                .ToListAsync();

            return Ok(activeSessions);
        }

        [HttpPost("revoke/{sessionId}")]
        public async Task<IActionResult> RevokeSessionAsync(int sessionId)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized();
            }

            var refreshToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Id == sessionId && rt.UserId == userId.Value);

            if (refreshToken == null)
            {
                return NotFound();
            }

            if (!refreshToken.IsRevoked)
            {
                refreshToken.IsRevoked = true;
                refreshToken.RevokedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }

            return Ok();
        }


        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> RegisterUserAsync([FromBody] CreateAccount createAccount)
        {
            if (createAccount.Password.Length < 10)
            {
                return BadRequest("Wachtwoord te kort");
            }

            var userSameEmail = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == createAccount.Email.ToLower());
            if (userSameEmail)
            {
                return BadRequest("Mailadres wordt al gebruikt");
            }
            var user = new User
            {
                Email = createAccount.Email.ToLower(),
                Guid = Guid.NewGuid()

            };
            
            var map = new Map
            {
                MapGuid = Guid.NewGuid(),
                Name = "Kaart",
                Default = true
            };
            map.User = user;
            user.Password = passwordHasher.HashPassword(user, createAccount.Password);
            _dbContext.Users.Add(user);
            _dbContext.Maps.Add(map);
            await _dbContext.SaveChangesAsync();
            return await LogUserIn(new LoginRequest { Email = createAccount.Email, Password = createAccount.Password });
        }
        
        /// <summary>
        /// Generates a JWT access token with the specified claims
        /// </summary>
        private string GenerateAccessToken(IEnumerable<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWTSigningKey"]));
            var validity = int.Parse(_configuration["Tokens:ValidityInMinutes"]);
            var jwt = new JwtSecurityToken(
                issuer: _configuration["Tokens:Issuer"],
                audience: "OVDB",
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(validity),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }

        /// <summary>
        /// Generates a cryptographically secure refresh token
        /// </summary>
        private async Task<RefreshToken> GenerateRefreshToken(int userId, string deviceInfo = null)
        {
            var token = GenerateSecureToken();
            
            // Extract device info from User-Agent header if not provided
            if (string.IsNullOrEmpty(deviceInfo))
            {
                deviceInfo = Request.Headers["User-Agent"].ToString();
                if (deviceInfo.Length > MaxDeviceInfoLength)
                {
                    var maxContentLength = MaxDeviceInfoLength - DeviceInfoTruncationSuffix.Length;
                    deviceInfo = deviceInfo.Substring(0, maxContentLength) + DeviceInfoTruncationSuffix;
                }
            }

            var refreshToken = new RefreshToken
            {
                Token = token,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
                DeviceInfo = deviceInfo,
                IsRevoked = false
            };

            _dbContext.RefreshTokens.Add(refreshToken);
            
            // Clean up expired tokens for this user
            await CleanupExpiredTokens(userId);
            
            return refreshToken;
        }

        /// <summary>
        /// Generates a cryptographically secure random token string
        /// </summary>
        private string GenerateSecureToken()
        {
            var randomBytes = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }

        /// <summary>
        /// Removes expired and revoked refresh tokens for a user
        /// </summary>
        private async Task CleanupExpiredTokens(int userId)
        {
            var expiredTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == userId && (rt.IsRevoked || rt.ExpiresAt < DateTime.UtcNow))
                .Where(rt => rt.RevokedAt == null || rt.RevokedAt < DateTime.UtcNow.AddDays(-RevokedTokenRetentionDays)) // Keep revoked tokens for audit
                .ToListAsync();

            if (expiredTokens.Any())
            {
                _dbContext.RefreshTokens.RemoveRange(expiredTokens);
            }
        }

        /// <summary>
        /// Helper method to extract user ID from JWT claims
        /// </summary>
        private int? GetUserIdFromClaims()
        {
            var subClaim = User.Claims.FirstOrDefault(s => s.Type == "sub");
            if (subClaim == null || string.IsNullOrEmpty(subClaim.Value))
            {
                return null;
            }

            if (int.TryParse(subClaim.Value, out var userId))
            {
                return userId;
            }

            return null;
        }
    }
}