using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TraewellingController : ControllerBase
    {
        private readonly ITrawellingService _trawellingService;
        private readonly OVDBDatabaseContext _dbContext;
        private readonly ILogger<TraewellingController> _logger;

        public TraewellingController(ITrawellingService trawellingService, 
            OVDBDatabaseContext dbContext, ILogger<TraewellingController> logger)
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

                var state = _trawellingService.GenerateAndStoreState(userId.Value);
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
        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleCallback([FromQuery] string code, [FromQuery] string state)
        {
            try
            {
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                {
                    return Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>Träwelling Connection</title>
</head>
<body>
    <script>
        window.opener?.postMessage({type: 'oauth-error', message: 'Missing parameters'}, '*');
        window.close();
    </script>
    <p>Invalid request. This window should close automatically.</p>
</body>
</html>", "text/html");
                }

                // Validate state parameter
                if (!_trawellingService.ValidateAndConsumeState(state,out var userId))
                {
                    return Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>Träwelling Connection</title>
</head>
<body>
    <script>
        window.opener?.postMessage({type: 'oauth-error', message: 'Invalid or expired state'}, '*');
        window.close();
    </script>
    <p>Invalid or expired request. This window should close automatically.</p>
</body>
</html>", "text/html");
                }

                var success = await _trawellingService.ExchangeCodeForTokensAsync(code, state, userId.Value);
                
                if (success)
                {
                    return Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>Träwelling Connection Successful</title>
</head>
<body>
    <script>
        window.opener?.postMessage({type: 'oauth-success', message: 'Träwelling account connected successfully'}, '*');
        window.close();
    </script>
    <p>Träwelling account connected successfully! This window should close automatically.</p>
</body>
</html>", "text/html");
                }

                return Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>Träwelling Connection</title>
</head>
<body>
    <script>
        window.opener?.postMessage({type: 'oauth-error', message: 'Failed to connect account'}, '*');
        window.close();
    </script>
    <p>Failed to connect Träwelling account. This window should close automatically.</p>
</body>
</html>", "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Träwelling OAuth callback");
                return Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>Träwelling Connection Error</title>
</head>
<body>
    <script>
        window.opener?.postMessage({type: 'oauth-error', message: 'Server error occurred'}, '*');
        window.close();
    </script>
    <p>An error occurred. This window should close automatically.</p>
</body>
</html>", "text/html");
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
        /// Get unimported trips from Träwelling (excluding ignored ones)
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

                var tripsResponse = await _trawellingService.GetOptimizedTripsAsync(user, page);
                
                if (tripsResponse == null)
                    return StatusCode(500, "Failed to fetch trips from Träwelling");

                return Ok(tripsResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unimported trips");
                return StatusCode(500, "Error fetching unimported trips");
            }
        }

        /// <summary>
        /// Ignore a Träwelling status so it doesn't show up in unimported list
        /// </summary>
        [HttpPost("ignore")]
        public async Task<IActionResult> IgnoreStatus([FromBody] TrawellingIgnoreRequest request)
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

                var success = await _trawellingService.IgnoreStatusAsync(user, request.StatusId);

                if (!success)
                    return BadRequest("Failed to ignore status or status already ignored");

                return Ok(new 
                { 
                    success = true, 
                    message = "Status ignored successfully" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ignoring status {StatusId}", request.StatusId);
                return StatusCode(500, "Error ignoring status");
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

        /// <summary>
        /// Get statistics about user's Träwelling integration
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
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
                    return Ok(new { connected = false });

                // Count RouteInstances linked to Träwelling
                var importedTripsCount = await _dbContext.RouteInstances
                    .Where(ri => ri.TrawellingStatusId.HasValue)
                    .CountAsync();

                // Count RouteInstances with timing data
                var tripsWithTimingCount = await _dbContext.RouteInstances
                    .Where(ri => ri.StartTime.HasValue && ri.EndTime.HasValue)
                    .CountAsync();

                // Count RouteInstances with source = traewelling
                var userTrawellingTripsCount = await _dbContext.RouteInstances
                    .Where(ri => ri.RouteInstanceProperties.Any(p => p.Key == "source" && p.Value.StartsWith("traewelling")))
                    .CountAsync();

                return Ok(new 
                { 
                    connected = true,
                    importedTripsCount = importedTripsCount,
                    tripsWithTimingCount = tripsWithTimingCount,
                    userTrawellingTripsCount = userTrawellingTripsCount
                });
            }
            catch (Exception ex)
            {
                var userId = GetCurrentUserId();
                _logger.LogError(ex, "Error getting Träwelling stats for user {UserId}", userId);
                return StatusCode(500, "Error retrieving statistics");
            }
        }

        /// <summary>
        /// Search for existing RouteInstances by date and optional search query
        /// </summary>
        [HttpGet("route-instances")]
        public async Task<IActionResult> SearchRouteInstances([FromQuery] string date, [FromQuery] string query = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                if (!DateTime.TryParse(date, out var searchDate))
                    return BadRequest("Invalid date format");

                var routeInstances = await _trawellingService.GetRouteInstancesByDateAsync(user, searchDate, query);

                var result = routeInstances.Select(ri => new
                {
                    id = ri.RouteInstanceId,
                    routeId = ri.RouteId,
                    routeName = ri.Route?.Name,
                    from = ri.Route?.From,
                    to = ri.Route?.To,
                    date = ri.Date,
                    startTime = ri.StartTime,
                    endTime = ri.EndTime,
                    durationHours = ri.DurationHours,
                    trawellingStatusId = ri.TrawellingStatusId,
                    hasTraewellingLink = ri.TrawellingStatusId.HasValue
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                var userId = GetCurrentUserId();
                _logger.LogError(ex, "Error searching RouteInstances for user {UserId}", userId);
                return StatusCode(500, "Error searching RouteInstances");
            }
        }

        /// <summary>
        /// Link a Träwelling status to an existing RouteInstance
        /// </summary>
        [HttpPost("link")]
        public async Task<IActionResult> LinkToRouteInstance([FromBody] LinkToRouteInstanceRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                var routeInstance = await _trawellingService.LinkStatusToRouteInstanceAsync(
                    user, request.StatusId, request.RouteInstanceId);

                if (routeInstance == null)
                    return BadRequest("Failed to link status to RouteInstance. Status may already be linked or RouteInstance may not exist.");

                return Ok(new 
                {
                    success = true,
                    routeInstance = new
                    {
                        id = routeInstance.RouteInstanceId,
                        routeName = routeInstance.Route?.Name,
                        startTime = routeInstance.StartTime,
                        endTime = routeInstance.EndTime,
                        trawellingStatusId = routeInstance.TrawellingStatusId
                    }
                });
            }
            catch (Exception ex)
            {
                var userId = GetCurrentUserId();
                _logger.LogError(ex, "Error linking Träwelling status to RouteInstance for user {UserId}", userId);
                return StatusCode(500, "Error linking status");
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            return null;
        }
    }
}