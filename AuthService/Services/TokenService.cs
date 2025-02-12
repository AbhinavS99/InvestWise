using StackExchange.Redis;

namespace AuthService.Services
{
    public class TokenService
    {
        private readonly IDatabase _redisDB;
        public TokenService(IConnectionMultiplexer redis)
        {
            _redisDB = redis.GetDatabase();
        }

        public async Task SetTokenAsync(string userId, string token, TimeSpan expiry)
        {
            await _redisDB.StringSetAsync($"userToken:{userId}", token, expiry);
        }

        public async Task<string?> GetTokenAsync(string userId)
        {
            var token = await _redisDB.StringGetAsync($"userToken:{userId}");
            return token.HasValue ? token.ToString() : null;
        }

        public async Task RemoveTokenAsync(string userId)
        {
            await _redisDB.KeyDeleteAsync($"userToken:{userId}");
        }
    }
}