using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OV_DB.Models;
using OV_DB.Services;
using System.Linq;
using System.Threading.Tasks;

namespace OV_DB.Controllers
{
    [Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExportController : ControllerBase
    {
        private readonly ITrainlogExportService _trainlogExportService;

        public ExportController(ITrainlogExportService trainlogExportService)
        {
            _trainlogExportService = trainlogExportService;
        }

        [HttpPost("Trainlog")]
        public async Task<IActionResult> ExportToTrainlog([FromBody] ExportRequest request)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }

            if ((request.RouteInstanceIds == null || !request.RouteInstanceIds.Any()) &&
                (request.RouteIds == null || !request.RouteIds.Any()))
            {
                return BadRequest("No routes selected");
            }

            var bytes = await _trainlogExportService.BuildTrainlogCsvAsync(User.GetUserId(), request);
            return File(bytes, "text/csv", "trainlog_export.csv");
        }
    }
}
