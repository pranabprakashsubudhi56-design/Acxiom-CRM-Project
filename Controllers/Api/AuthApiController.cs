using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public AuthApiController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new LoginResponseDto { Succeeded = false, Message = "Invalid request payload." });
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !user.IsActive)
        {
            await _auditService.LogAsync("Failed Login", "Auth", null, null, new { model.Email }, "Failed", "API login attempt failed: user not found or inactive.");
            return Unauthorized(new LoginResponseDto { Succeeded = false, Message = "Invalid credentials or account inactive." });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await _auditService.LogAsync("Login", "Auth", user.Id, null, null, "Success", "User logged in via REST API.");
            return Ok(new LoginResponseDto
            {
                Succeeded = true,
                Message = "Authentication successful.",
                UserId = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                Role = user.UserRole
            });
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogAsync("Failed Login", "Auth", user.Id, null, null, "Failed", "API Login: Account is locked out.");
            return StatusCode(StatusCodes.Status403Forbidden, new LoginResponseDto { Succeeded = false, Message = "Account is temporarily locked out." });
        }

        await _auditService.LogAsync("Failed Login", "Auth", user.Id, null, null, "Failed", "API Login: Incorrect password.");
        return Unauthorized(new LoginResponseDto { Succeeded = false, Message = "Invalid credentials." });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { message = "Logged out successfully." });
    }
}
