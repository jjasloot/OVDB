using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OV_DB.Models;
using OV_DB.Services;
using OVDB_database.Database;
using OVDB_database.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
        private readonly ITraewellingRateLimiter _rateLimiter;

        public TraewellingController(ITrawellingService trawellingService,
            OVDBDatabaseContext dbContext, ILogger<TraewellingController> logger, ITraewellingRateLimiter rateLimiter)
        {
            _trawellingService = trawellingService;
            _dbContext = dbContext;
            _logger = logger;
            _rateLimiter = rateLimiter;
        }

        /// <summary>
        /// Get OAuth2 authorization URL for connecting Träwelling account
        /// </summary>
        [HttpGet("connect")]
        public IActionResult GetConnectUrl([FromQuery] bool liveSync = false)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var state = _trawellingService.GenerateAndStoreState(userId.Value);
                var authUrl = _trawellingService.GetAuthorizationUrl(userId.Value, state, liveSync);

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
        /// Receiver for Träwelling webhook deliveries (live sync). Anonymous: authenticated
        /// by the per-webhook HMAC secret issued during the OAuth token exchange.
        /// </summary>
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> ReceiveWebhook()
        {
            if (!_trawellingService.IsLiveSyncAvailable)
                return NotFound();

            // StatusResource payloads are small; anything bigger is not a genuine delivery
            if (Request.ContentLength > 1024 * 1024)
                return StatusCode(StatusCodes.Status413PayloadTooLarge);

            if (!Request.Headers.TryGetValue("X-Trwl-Webhook-Id", out var webhookIdHeader)
                || !int.TryParse(webhookIdHeader.FirstOrDefault(), out var webhookId))
                return Unauthorized();
            if (!Request.Headers.TryGetValue("Signature", out var signatureHeader))
                return Unauthorized();

            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }
            if (string.IsNullOrEmpty(body))
                return BadRequest();

            var user = await _dbContext.Users
                .SingleOrDefaultAsync(u => u.TraewellingWebhookId == webhookId);
            if (user == null || string.IsNullOrEmpty(user.TraewellingWebhookSecret))
                return Unauthorized();

            // Spatie's DefaultSigner: lowercase hex HMAC-SHA256 of the raw JSON body
            var computed = Convert.ToHexString(
                    HMACSHA256.HashData(
                        Encoding.UTF8.GetBytes(user.TraewellingWebhookSecret),
                        Encoding.UTF8.GetBytes(body)))
                .ToLowerInvariant();
            var provided = signatureHeader.ToString().Trim().ToLowerInvariant();
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computed),
                    Encoding.UTF8.GetBytes(provided)))
            {
                _logger.LogWarning("Träwelling webhook delivery with invalid signature for webhook {WebhookId}", webhookId);
                return Unauthorized();
            }

            // From here on always return 200: Träwelling counts non-2xx responses as failed
            // deliveries and auto-disables the webhook after a few. A processing bug must not
            // kill the subscription — the sweep heals whatever a swallowed error missed.
            try
            {
                await _trawellingService.ProcessWebhookEventAsync(user, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Träwelling webhook delivery for user {UserId}", user.Id);
            }
            return Ok();
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

                if (!_trawellingService.IsConnected(user))
                {
                    return Ok(new { connected = false, liveSyncAvailable = _trawellingService.IsLiveSyncAvailable });
                }

                // This refreshes the access token when it has expired; a definitive refresh
                // failure clears the stored tokens, so re-check afterwards
                var userInfo = await _trawellingService.GetUserInfoAsync(user);

                if (userInfo == null && !_trawellingService.IsConnected(user))
                {
                    return Ok(new { connected = false, liveSyncAvailable = _trawellingService.IsLiveSyncAvailable });
                }

                var webhookHealth = TrawellingWebhookHealth.NotEnabled;
                if (_trawellingService.IsLiveSyncAvailable && user.TraewellingWebhookId.HasValue)
                {
                    webhookHealth = await _trawellingService.GetWebhookHealthAsync(user);
                }

                return Ok(new
                {
                    connected = true,
                    user = userInfo,
                    liveSyncAvailable = _trawellingService.IsLiveSyncAvailable,
                    liveSyncEnabled = user.TraewellingWebhookId.HasValue,
                    liveSyncHealth = webhookHealth.ToString()
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
        public async Task<IActionResult> GetUnimportedTrips([FromQuery] int page = 1, [FromQuery] bool refresh = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                if (!_trawellingService.IsConnected(user))
                    return BadRequest("Träwelling account not connected or tokens expired");

                var tripsResponse = await _trawellingService.GetOptimizedTripsAsync(user, page, refresh, cancellationToken);

                if (tripsResponse == null && _rateLimiter.IsLimited)
                    return StatusCode(429, "Träwelling is rate limiting us, please try again in a minute");

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

                if (!_trawellingService.IsConnected(user))
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

                // Remove the webhook first — deleting it upstream needs the tokens
                await _trawellingService.RemoveWebhookAsync(user);

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
        /// Imported trips whose Träwelling status changed or disappeared upstream
        /// </summary>
        [HttpGet("conflicts")]
        public async Task<IActionResult> GetConflicts()
        {
            var user = await GetAuthorizedUserAsync();
            if (user == null)
                return Unauthorized();

            try
            {
                return Ok(await _trawellingService.GetConflictsAsync(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Träwelling conflicts");
                return StatusCode(500, "Error fetching conflicts");
            }
        }

        [HttpPost("conflicts/{statusId:int}/apply-times")]
        public async Task<IActionResult> ApplyConflictTimes(int statusId)
        {
            var user = await GetAuthorizedUserAsync();
            if (user == null)
                return Unauthorized();

            return await _trawellingService.ApplyConflictTimesAsync(user, statusId)
                ? Ok(new { success = true })
                : BadRequest("Conflict not found or times could not be applied");
        }

        [HttpPost("conflicts/{statusId:int}/reimport")]
        public async Task<IActionResult> ReimportConflict(int statusId)
        {
            var user = await GetAuthorizedUserAsync();
            if (user == null)
                return Unauthorized();

            return await _trawellingService.ReimportConflictAsync(user, statusId)
                ? Ok(new { success = true })
                : BadRequest("Conflict not found");
        }

        [HttpPost("conflicts/{statusId:int}/dismiss")]
        public async Task<IActionResult> DismissConflict(int statusId)
        {
            var user = await GetAuthorizedUserAsync();
            if (user == null)
                return Unauthorized();

            return await _trawellingService.DismissConflictAsync(user, statusId)
                ? Ok(new { success = true })
                : BadRequest("Conflict not found");
        }

        [HttpPost("conflicts/{statusId:int}/delete-instance")]
        public async Task<IActionResult> DeleteInstanceForConflict(int statusId)
        {
            var user = await GetAuthorizedUserAsync();
            if (user == null)
                return Unauthorized();

            return await _trawellingService.DeleteInstanceForConflictAsync(user, statusId)
                ? Ok(new { success = true })
                : BadRequest("Conflict not found");
        }

        private async Task<User> GetAuthorizedUserAsync()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return null;
            return await _dbContext.Users.FindAsync(userId.Value);
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

                if (!_trawellingService.IsConnected(user))
                    return Ok(new { connected = false });

                // All counts are scoped to the current user's routes/maps.
                System.Linq.Expressions.Expression<Func<RouteInstance, bool>> ownedByUser =
                    ri => ri.RouteInstanceMaps.Any(rim => rim.Map.UserId == user.Id)
                        || ri.Route.RouteMaps.Any(rm => rm.Map.UserId == user.Id);

                // Count RouteInstances linked to Träwelling
                var importedTripsCount = await _dbContext.RouteInstances
                    .Where(ownedByUser)
                    .Where(ri => ri.TrawellingStatusId.HasValue)
                    .CountAsync();

                // Count RouteInstances with timing data
                var tripsWithTimingCount = await _dbContext.RouteInstances
                    .Where(ownedByUser)
                    .Where(ri => ri.StartTime.HasValue && ri.EndTime.HasValue)
                    .CountAsync();

                // Count RouteInstances with source = traewelling
                var userTrawellingTripsCount = await _dbContext.RouteInstances
                    .Where(ownedByUser)
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

        /// <summary>
        /// Fetch active warning/danger alerts from the Träwelling platform
        /// </summary>
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                if (!_trawellingService.IsConnected(user))
                    return Ok(Array.Empty<object>());

                var alerts = await _trawellingService.GetAlertsAsync(user);
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Träwelling alerts");
                return StatusCode(500, "Error fetching alerts");
            }
        }

        /// <summary>
        /// Backfill scheduled (planned) departure/arrival times for existing Träwelling-imported trips
        /// </summary>
        [HttpPost("backfill-scheduled")]
        public async Task<IActionResult> BackfillScheduledTimes()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized();

                var user = await _dbContext.Users.FindAsync(userId.Value);
                if (user == null)
                    return NotFound("User not found");

                var (found, updated, failed) = await _trawellingService.BackfillScheduledTimesAsync(user);

                return Ok(new { found, updated, failed });
            }
            catch (Exception ex)
            {
                var userId = GetCurrentUserId();
                _logger.LogError(ex, "Error backfilling scheduled times for user {UserId}", userId);
                return StatusCode(500, "Error backfilling scheduled times");
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