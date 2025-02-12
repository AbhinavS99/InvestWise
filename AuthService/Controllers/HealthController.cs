using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using AuthService.Data;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthCheckController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IConnectionMultiplexer _redis;

        public HealthCheckController(AppDbContext dbContext, IConnectionMultiplexer redis)
        {
            _dbContext = dbContext;
            _redis = redis;
        }

        [HttpGet]
        public async Task<IActionResult> HealthCheck()
        {
            var healthReport = new
            {
                DatabaseConnected = await IsDatabaseConnected(),
                RedisConnected = IsRedisConnected()
            };

            if (!healthReport.DatabaseConnected || !healthReport.RedisConnected)
            {
                return StatusCode(500, healthReport);
            }

            return Ok(healthReport);
        }

        private async Task<bool> IsDatabaseConnected()
        {
            try
            {
                return await _dbContext.Database.CanConnectAsync();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool IsRedisConnected()
        {
            try
            {
                return _redis.IsConnected;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
