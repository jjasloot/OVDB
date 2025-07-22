using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OV_DB.Models;
using OVDB_database.Database;
using OVDB_database.Models;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        public OVDBDatabaseContext DatabaseContext { get; }
        private readonly PasswordHasher<User> passwordHasher;

        public UserController(OVDBDatabaseContext databaseContext)
        {
            DatabaseContext = databaseContext;
            passwordHasher = new PasswordHasher<User>();
        }

        [HttpGet("profile")]
        public async Task<ActionResult<UserProfileDTO>> GetProfileAsync()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var user = await DatabaseContext.Users.FindAsync(userIdClaim);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(new UserProfileDTO
            {
                Email = user.Email,
                PreferredLanguage = user.PreferredLanguage ?? "en",
                TelegramUserId = user.TelegramUserId
            });
        }

        [HttpPut("profile")]
        public async Task<ActionResult> UpdateProfileAsync([FromBody] UpdateProfileDTO updateProfile)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var user = await DatabaseContext.Users.FindAsync(userIdClaim);
            if (user == null)
            {
                return NotFound();
            }

            // Validate language preference
            if (updateProfile.PreferredLanguage != "en" && updateProfile.PreferredLanguage != "nl")
            {
                return BadRequest("Invalid language preference. Must be 'en' or 'nl'.");
            }

            user.PreferredLanguage = updateProfile.PreferredLanguage;
            user.TelegramUserId = updateProfile.TelegramUserId;

            DatabaseContext.Update(user);
            await DatabaseContext.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("change-password")]
        public async Task<ActionResult> ChangePasswordAsync([FromBody] ChangePasswordDTO changePassword)
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }

            var user = await DatabaseContext.Users.FindAsync(userIdClaim);
            if (user == null)
            {
                return NotFound();
            }

            // Verify current password
            var currentPasswordResult = passwordHasher.VerifyHashedPassword(user, user.Password, changePassword.CurrentPassword);
            if (currentPasswordResult != PasswordVerificationResult.Success && currentPasswordResult != PasswordVerificationResult.SuccessRehashNeeded)
            {
                return BadRequest("Current password is incorrect.");
            }

            // Validate new password length
            if (changePassword.NewPassword.Length < 10)
            {
                return BadRequest("New password must be at least 10 characters long.");
            }

            // Update password
            user.Password = passwordHasher.HashPassword(user, changePassword.NewPassword);
            DatabaseContext.Update(user);
            await DatabaseContext.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("maps")]
        public async Task<ActionResult<List<Map>>> GetMapsAsync()
        {
            var userIdClaim = int.Parse(User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value ?? "-1");
            if (userIdClaim < 0)
            {
                return Forbid();
            }
            var maps = await DatabaseContext.Maps.Where(m => m.UserId == userIdClaim).ToListAsync();
            return Ok(maps);
        }

        [HttpGet("link/{name}")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> GetGuidFromNameAsync(string name)
        {
            var map = await DatabaseContext.Maps.Where(m => m.SharingLinkName == name).SingleOrDefaultAsync();
            if (map == null)
            {
                return NotFound();
            }
            return Ok(map.MapGuid);
        } 

        [HttpGet("station-link/{name}")]
        [AllowAnonymous]
        public async Task<ActionResult<string>> GetGuidFromNameForStationsAsync(string name)
        {
            var map = await DatabaseContext.StationMaps.Where(m => m.SharingLinkName == name).SingleOrDefaultAsync();
            if (map == null)
            {
                return NotFound();
            }
            return Ok(map.MapGuid);
        }

        //[HttpGet("overwriteGUIDs")]
        //[AllowAnonymous]
        //public async Task<ActionResult> ResetAllGuids()
        //{
        //    var maps = await DatabaseContext.Maps.ToListAsync();
        //    Guid.NewGuid();
        //    maps.ForEach(m => m.MapGuid = Guid.NewGuid());
        //    DatabaseContext.UpdateRange(maps);
        //    await DatabaseContext.SaveChangesAsync();
        //    return Ok();
        //}
    }
}