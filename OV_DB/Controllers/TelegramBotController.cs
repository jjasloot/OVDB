using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OV_DB.Services;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class TelegramBotController : ControllerBase
    {
        // Telegram sends this header when the webhook is registered with a secret_token.
        private const string TelegramSecretHeader = "X-Telegram-Bot-Api-Secret-Token";

        private readonly TelegramBotService _telegramBotService;
        private readonly IConfiguration _configuration;

        public TelegramBotController(TelegramBotService telegramBotService, IConfiguration configuration)
        {
            _telegramBotService = telegramBotService;
            _configuration = configuration;
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] Update update)
        {
            // Reject forged webhook calls. The endpoint fails closed: without a configured
            // secret there is no way to distinguish Telegram from an attacker (who can add or
            // remove StationVisits for any user by forging callbackQuery.From.Id), so refuse.
            var expectedSecret = _configuration["Telegram:WebhookSecret"];
            if (string.IsNullOrEmpty(expectedSecret))
            {
                return Unauthorized();
            }

            var provided = Request.Headers[TelegramSecretHeader].ToString();
            var providedBytes = Encoding.UTF8.GetBytes(provided);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
            if (!CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                return Unauthorized();
            }

            await _telegramBotService.HandleUpdateAsync(update);
            return Ok();
        }
    }
}
