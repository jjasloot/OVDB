using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;

namespace OV_DB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TrawellingController : ControllerBase
    {
        private readonly ITrawellingService _trawellingService;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly ILogger<TrawellingController> _logger;

        public TrawellingController(ITrawellingService trawellingService, 
            OVDBDatabaseContext dbContext, ILogger<TrawellingController> logger)
        {
            _trawellingService = trawellingService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Get OAuth2 authorization URL for connecting Träwelling account
        /// </summary>
        [HttpGet("connect")]
        public IActionResult GetConnectUrl()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var state = Guid.NewGuid().ToString();
                // TODO: Store state in a more secure way (cache, database, etc.)
                // For now, we'll pass it back and validate on callback
                // HttpContext.Session.SetString($"traewelling_oauth_state_{userId}", state);

                var authUrl = _trawellingService.GetAuthorizationUrl(userId.Value, state);
                
                return Ok(new { authorizationUrl = authUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Träwelling connect URL");
                return StatusCode(500, "Error generating connect URL");
            }
        }

        /// <summary>
        /// Handle OAuth2 callback from Träwelling
        /// </summary>
        [HttpPost("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleCallback([FromBody] TrawellingOAuthRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.State))
                    return BadRequest("Code and state are required");

                // TODO: Implement proper state validation
                // For now, we'll accept any state and extract user from JWT token
                // var sessionState = HttpContext.Session.GetString($"traewelling_oauth_state_{userId}");
                // if (sessionState != request.State)
                //     return BadRequest("Invalid state parameter");

                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var success = await _trawellingService.ExchangeCodeForTokensAsync(request.Code, request.State, userId.Value);
                
                if (success)
                {
                    // HttpContext.Session.Remove($"traewelling_oauth_state_{userId}");
                    return Ok(new { success = true, message = "Träwelling account connected successfully" });
                }

                return BadRequest("Failed to connect Träwelling account");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Träwelling OAuth callback");
                return StatusCode(500, "Error processing callback");
            }
        }

        /// <summary>
        /// Get connection status and user info from Träwelling
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetConnectionStatus()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                var isConnected = _trawellingService.HasValidTokens(user);
                
                if (!isConnected)
                {
                    return Ok(new { connected = false });
                }

                var userInfo = await _trawellingService.GetUserInfoAsync(user);
                
                return Ok(new 
                { 
                    connected = true, 
                    user = userInfo 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Träwelling connection status");
                return StatusCode(500, "Error checking connection status");
            }
        }

        /// <summary>
        /// Get unimported trips from Träwelling
        /// </summary>
        [HttpGet("unimported")]
        public async Task<IActionResult> GetUnimportedTrips([FromQuery] int page = 1)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                if (!_trawellingService.HasValidTokens(user))
                    return BadRequest("Träwelling account not connected or tokens expired");

                var statusesResponse = await _trawellingService.GetUnimportedStatusesAsync(user, page);
                
                if (statusesResponse == null)
                    return StatusCode(500, "Failed to fetch trips from Träwelling");

                return Ok(statusesResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unimported trips");
                return StatusCode(500, "Error fetching unimported trips");
            }
        }

        /// <summary>
        /// Import a specific trip from Träwelling
        /// </summary>
        [HttpPost("import")]
        public async Task<IActionResult> ImportTrip([FromBody] TrawellingImportRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                if (!_trawellingService.HasValidTokens(user))
                    return BadRequest("Träwelling account not connected or tokens expired");

                var importedTrip = await _trawellingService.ImportStatusAsync(
                    user, request.StatusId, request.ImportMetadata, request.ImportTags);

                if (importedTrip == null)
                    return BadRequest("Failed to import trip");

                return Ok(new 
                { 
                    success = true, 
                    routeInstanceId = importedTrip.RouteInstanceId,
                    message = "Trip imported successfully" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing trip {StatusId}", request.StatusId);
                return StatusCode(500, "Error importing trip");
            }
        }

        /// <summary>
        /// Process backlog of trips from Träwelling
        /// </summary>
        [HttpPost("process-backlog")]
        public async Task<IActionResult> ProcessBacklog([FromQuery] int maxPages = 5)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                if (!_trawellingService.HasValidTokens(user))
                    return BadRequest("Träwelling account not connected or tokens expired");

                var processedCount = await _trawellingService.ProcessBacklogAsync(user, maxPages);

                return Ok(new 
                { 
                    success = true, 
                    processedCount = processedCount,
                    message = $"Processed {processedCount} trips from backlog" 
                });
            }
            catch (Exception ex)
            {
                var userId = GetCurrentUserId();
                _logger.LogError(ex, "Error processing backlog for user {UserId}", userId);
                return StatusCode(500, "Error processing backlog");
            }
        }

        /// <summary>
        /// Disconnect Träwelling account by removing stored tokens
        /// </summary>
        [HttpDelete("disconnect")]
        public async Task<IActionResult> DisconnectAccount()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                user.TrawellingAccessToken = null;
                user.TrawellingRefreshToken = null;
                user.TrawellingTokenExpiresAt = null;

                await _dbContext.SaveChangesAsync();

                return Ok(new { success = true, message = "Träwelling account disconnected" });
            }
            catch (Exception ex)
            {
                var userId = GetCurrentUserId();
                _logger.LogError(ex, "Error disconnecting Träwelling account for user {UserId}", userId);
                return StatusCode(500, "Error disconnecting account");
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(x => x.Type == "UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return null;
        }
    }
}