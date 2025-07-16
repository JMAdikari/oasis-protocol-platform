using AuthBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _authService.RegisterAsync(model);

                // Check if result is an error message
                if (result == "User already exists" || result.Contains("Password") || result.Contains("Email"))
                {
                    return BadRequest(new { message = result });
                }

                // If we get here, registration was successful and result is the JWT token
                var user = await _authService.GetUserByEmailAsync(model.Email);
                return Ok(new
                {
                    message = "Registration successful",
                    token = result,
                    user = new
                    {
                        email = model.Email,
                        fullName = $"{model.FirstName} {model.LastName}",
                        role = user?.Role ?? "User"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred during registration", error = ex.Message });
            }
        }



        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _authService.LoginAsync(model);

                if (result == "Invalid credentials")
                {
                    return Unauthorized(new { message = "Invalid email or password" });
                }

                // Get user details for response
                var user = await _authService.GetUserByEmailAsync(model.Email);

                return Ok(new
                {
                    message = "Login successful",
                    token = result,
                    user = new
                    {
                        email = user?.Email,
                        firstName = user?.FirstName,
                        role = user?.Role
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred during login", error = ex.Message });
            }
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                
                if (string.IsNullOrEmpty(userEmail))
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var user = await _authService.GetUserByEmailAsync(userEmail);
                
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(new
                {
                    id = user.Id,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    userName = user.UserName,
                    role = user.Role
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving profile", error = ex.Message });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            // Extract the JWT token from the Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
            return BadRequest(new { message = "No token provided" });
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            // Blacklist the token using the auth service
            var result = await _authService.BlacklistTokenAsync(token);

            if (!result)
            {
            return StatusCode(500, new { message = "Failed to blacklist token" });
            }

            return Ok(new { message = "Logout successful" });
        }

        [HttpGet("validate-token")]
        [Authorize]
        public IActionResult ValidateToken()
        {
            // If the request reaches here, the token is valid (due to [Authorize] attribute)
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var firstName = User.FindFirst("FirstName")?.Value;
            var lastName = User.FindFirst("LastName")?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "Token is valid",
                user = new
                {
                    email = userEmail,
                    firstName = firstName,
                    lastName = lastName,
                    role = role
                }
            });
        }

        [HttpGet("check-admin")]
        [Authorize(Roles = "Admin")]
        public IActionResult CheckAdminAccess()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "Admin access confirmed",
                user = new
                {
                    email = userEmail,
                    role = role,
                    hasAdminAccess = true
                }
            });
        }

        [HttpGet("check-user")]
        [Authorize(Roles = "User,Admin")]
        public IActionResult CheckUserAccess()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            return Ok(new
            {
                message = "User access confirmed",
                user = new
                {
                    email = userEmail,
                    role = role,
                    hasUserAccess = true
                }
            });
        }
    }
}