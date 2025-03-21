using Microsoft.AspNetCore.Mvc;
using OV_DB.Services;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace OV_DB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TelegramBotController : ControllerBase
    {
        private readonly TelegramBotService _telegramBotService;

        public TelegramBotController(TelegramBotService telegramBotService)
        {
            _telegramBotService = telegramBotService;
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] Update update)
        {
            await _telegramBotService.HandleUpdateAsync(update);
            return Ok();
        }
    }
}
