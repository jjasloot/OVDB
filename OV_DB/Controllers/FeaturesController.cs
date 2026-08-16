using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OV_DB.Services;

namespace OV_DB.Controllers;

/// <summary>
/// What the signed-in user is allowed to see, so the frontend can hide navigation for features
/// that are switched off. The endpoints themselves enforce the same rules independently.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class FeaturesController(IFeatureService featureService) : ControllerBase
{
    [HttpGet]
    public ActionResult<FeaturesDTO> Get()
    {
        return Ok(new FeaturesDTO
        {
            Achievements = featureService.IsVisible(featureService.Achievements, User.IsAdmin())
        });
    }

    public class FeaturesDTO
    {
        public bool Achievements { get; set; }
    }
}
