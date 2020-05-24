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

            var token = GenerateToken(claims);

            user.LastLogin = DateTime.UtcNow;
            _dbContext.Update(user);
            await _dbContext.SaveChangesAsync();

            return new LoginResponse { Token = token, RefreshToken = null };
        }

        [HttpPost("refreshToken")]
        public ActionResult<LoginResponse> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var claims = User.Claims.Where(s => s.Type != "aud");
            var newJwtToken = GenerateToken(claims);

            return new LoginResponse
            {
                Token = newJwtToken,
                RefreshToken = null
            };
        }


        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> RegisterUserAsync([FromBody] CreateAccount createAccount)
        {
            if (createAccount.Password.Length < 10)
            {
                return BadRequest("Wachtwoord te kort");
            }
            var inviteCode = await _dbContext.InviteCodes.FirstOrDefaultAsync(c => c.Code == createAccount.InviteCode && c.IsUsed == false);

            if (inviteCode == null)
            {
                return BadRequest("Ongeldige invite code");
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
            if (inviteCode.DoesNotExpire == false)
            {
                inviteCode.IsUsed = true;
                inviteCode.User = user;
            }
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
        private string GenerateToken(IEnumerable<Claim> claims)
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

    }
}