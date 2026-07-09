using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OV_DB.Services;
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
            // Reject forged webhook calls. Enforced only when a secret is configured so the
            // endpoint keeps working until the webhook is (re)registered with a matching secret_token.
            var expectedSecret = _configuration["Telegram:WebhookSecret"];
            if (!string.IsNullOrEmpty(expectedSecret))
            {
                var provided = Request.Headers[TelegramSecretHeader].ToString();
                if (!string.Equals(provided, expectedSecret, System.StringComparison.Ordinal))
                {
                    return Unauthorized();
                }
            }

            await _telegramBotService.HandleUpdateAsync(update);
            return Ok();
        }
    }
}
