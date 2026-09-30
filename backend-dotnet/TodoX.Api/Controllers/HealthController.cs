using Microsoft.AspNetCore.Mvc;

namespace TodoX.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", time = timeProvider.GetUtcNow().UtcDateTime });
}
