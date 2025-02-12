using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers
{
    [ApiController]
    [Route("/api/health")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;
        public HealthController(ILogger<HealthController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult GetHealthCheck()
        {
            _logger.LogInformation("Health Check Requested.");
            _logger.LogInformation("Gateway Healthy");
            return Ok(new {detail = "Gateway Healthy"});
        }
    }
}