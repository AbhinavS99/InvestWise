using Microsoft.AspNetCore.Mvc;
using AuthService.Models;
using Microsoft.AspNetCore.Identity;
using AuthService.Services;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;


namespace AuthService.Controllers
{
    [ApiController]
    [Route("/api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;
        private readonly UserManager<AppUser> _userManager;
        private readonly JwtService _jwtService;
        private readonly TokenService _tokenService;
        public AuthController(ILogger<AuthController> logger, UserManager<AppUser> userManager, JwtService jwtService, TokenService tokenService)
        {
            _logger = logger;
            _userManager = userManager;
            _jwtService = jwtService;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            _logger.LogInformation($"Register Request received for => {model.FirstName}, {model.LastName}, {model.Email}");
            if (!ModelState.IsValid)
            {
                _logger.LogError($"ModelState Error => {ModelState}");
                return BadRequest(ModelState);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return BadRequest(new { detail = "User with this email already exists." });
            }

            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
            };
            
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded) 
            {
                return Ok(new { detail = "User Registered Successfully." });
            }
            return BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            _logger.LogInformation($"Login Request received for => {model.Email}");
            if (!ModelState.IsValid) 
            {
                _logger.LogError($"ModelState Error => {ModelState}");
                return BadRequest(ModelState);
            }
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return NotFound(new {detail = "User does not exist."});
            }

            if(!await _userManager.CheckPasswordAsync(user, model.Password))
            {
                return Unauthorized(new { detail = "Invalid email or password." });
            }

            var existingToken = await _tokenService.GetTokenAsync(user.Id);
            if (!string.IsNullOrEmpty(existingToken))
            {
                return Ok(new { detail = "User already logged in.", token = existingToken });
            }

            var token = _jwtService.GenerateJwtToken(user);
            var jwtToken = _jwtService.DecodeJwtToken(token);
            var expiry = jwtToken.ValidTo;
            await _tokenService.SetTokenAsync(user.Id, token, expiry - DateTime.UtcNow);
            
            return Ok(new { detail = "User logged in Successfully.", token });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("Logout Requested");
            var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (token == null) return BadRequest(new { detail = "Token is required for logout." });
            try
            {
                var jwtToken = _jwtService.DecodeJwtToken(token);
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
                {
                    return Unauthorized(new { detail = "Invalid token." });
                }
                var redisRefToken = await _tokenService.GetTokenAsync(userId);
                if (string.IsNullOrEmpty(redisRefToken))
                {
                    return Unauthorized(new { detail = "User Already Logged Out." }); ;
                }
                _logger.LogInformation($"Logging out user: {email} (ID: {userId})");
                await _tokenService.RemoveTokenAsync(userId);
                return Ok(new { detail = "Logged out successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error decoding token: {ex.Message}");
                return Unauthorized(new { detail = "Invalid token format." });
            }
        }

        [HttpGet("validate-token")]
        public async Task<IActionResult> ValidateToken()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (string.IsNullOrEmpty(authHeader)) return Unauthorized(new { detail = "Header is missing." });

            var token = authHeader.Split(" ").Last();
            if (token == null) return Unauthorized(new { detail = "Token is missing." });

            try
            {
                var jwtToken = _jwtService.DecodeJwtToken(token);
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
                {
                    return Unauthorized(new { detail = "Invalid token." });
                }
                var redisRefToken = await _tokenService.GetTokenAsync(userId);
                if (string.IsNullOrEmpty(redisRefToken))
                {
                    return Unauthorized(new { detail = "Not Authorized, Log In Again." }); ;
                }
                var isValid = _jwtService.ValidateJwtToken(token);
                return isValid ? Ok(new { valid = true }) : Unauthorized(new { valid = false });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error decoding token: {ex.Message}");
                return Unauthorized(new { detail = "Invalid token format." });
            }
        }
    }
}