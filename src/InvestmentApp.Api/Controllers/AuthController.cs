using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace InvestmentApp.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;

    public AuthController(
        IUserRepository userRepository,
        IJwtService jwtService,
        IAuditService auditService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _auditService = auditService;
        _configuration = configuration;
    }

    [HttpGet("google/login")]
    public IActionResult GoogleLogin()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("GoogleCallback"),
            Items =
            {
                { "LoginProvider", "Google" }
            }
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Processes the authenticated user after Google OAuth middleware completes.
    /// This route MUST be different from CallbackPath to avoid middleware interception loop.
    /// </summary>
    [HttpGet("google/process-callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        try
        {
            // Log the start of callback processing
            await _auditService.LogAsync(new AuditEntry
            {
                UserId = "system",
                Action = "Debug",
                EntityId = "auth",
                EntityType = "System",
                Description = "Google OAuth callback started"
            });

            // Authenticate using Cookie scheme - the middleware already signed in via cookies
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded)
            {
                await _auditService.LogAsync(new AuditEntry
                {
                    UserId = "system",
                    Action = "Error",
                    EntityId = "auth",
                    EntityType = "System",
                    Description = $"Google authentication failed: {authenticateResult.Failure?.Message}"
                });

                return BadRequest(new
                {
                    error = "Google authentication failed",
                    details = authenticateResult.Failure?.Message
                });
            }

            var claims = authenticateResult.Principal.Claims;
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var googleId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var picture = claims.FirstOrDefault(c => c.Type == "picture")?.Value;

            // Log claims received
            await _auditService.LogAsync(new AuditEntry
            {
                UserId = "system",
                Action = "Debug",
                EntityId = "auth",
                EntityType = "System",
                Description = $"Google claims received - Email: {email}, Name: {name}, GoogleId: {googleId}"
            });

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(googleId))
            {
                return BadRequest(new
                {
                    error = "Invalid Google authentication response",
                    details = "Missing required claims: email, name, or googleId"
                });
            }

            // Check if user exists
            var existingUser = await _userRepository.GetByEmailAsync(email);
            User user;

            if (existingUser == null)
            {
                // Create new user
                user = new User(email, name, picture, "google");
                user.RecordLogin();
                await _userRepository.AddAsync(user);

                await _auditService.LogAsync(new AuditEntry
                {
                    UserId = user.Id,
                    Action = "Created",
                    EntityId = user.Id,
                    EntityType = "User",
                    Description = $"New user registered via Google OAuth: {email}"
                });
            }
            else
            {
                user = existingUser;
                var profileChanged = user.Name != name || user.Avatar != picture;
                if (profileChanged)
                {
                    user.UpdateProfile(name, picture);
                }
                user.RecordLogin();
                await _userRepository.UpdateAsync(user);
            }

            // Generate JWT token
            var token = _jwtService.GenerateToken(user);

            await _auditService.LogAsync(new AuditEntry
            {
                UserId = user.Id,
                Action = "Login",
                EntityId = user.Id,
                EntityType = "User",
                Description = $"User logged in via Google OAuth: {email}"
            });

            // For API clients, return JSON response
            if (Request.Headers.Accept.ToString().Contains("application/json") ||
                Request.Headers["X-Requested-With"].ToString().Contains("XMLHttpRequest"))
            {
                return Ok(new
                {
                    success = true,
                    token = token,
                    user = new
                    {
                        user.Id,
                        user.Email,
                        user.Name,
                        user.Avatar,
                        user.Provider,
                        user.CreatedAt
                    }
                });
            }

            // For web browsers, redirect to frontend
            var frontendBaseUrl = _configuration["FrontendUrl"] ?? "http://localhost:4200";
            var frontendUrl = $"{frontendBaseUrl}/auth/callback?token={token}";
            return Redirect(frontendUrl);
        }
        catch (Exception ex)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                UserId = "system",
                Action = "Error",
                EntityId = "auth",
                EntityType = "System",
                Description = $"Google authentication error: {ex.Message}"
            });

            return StatusCode(500, new
            {
                error = "Authentication error",
                details = ex.Message
            });
        }
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.Name,
            user.Avatar,
            user.Provider,
            user.CreatedAt
        });
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await _auditService.LogAsync(new AuditEntry
                {
                    UserId = userId,
                    Action = "Logout",
                    EntityId = userId,
                    EntityType = "User",
                    Description = "User logged out"
                });
            }

            // Sign out from cookie authentication
            await HttpContext.SignOutAsync();

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Logout error",
                details = ex.Message
            });
        }
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return NotFound();

            var newToken = _jwtService.GenerateToken(user);

            return Ok(new
            {
                token = newToken,
                user = new
                {
                    user.Id,
                    user.Email,
                    user.Name,
                    user.Avatar,
                    user.Provider,
                    user.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Token refresh error",
                details = ex.Message
            });
        }
    }
}