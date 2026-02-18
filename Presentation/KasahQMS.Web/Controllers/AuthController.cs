using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Identity.Commands;
using KasahQMS.Application.Features.Identity.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasahQMS.Web.Controllers;

/// <summary>
/// Authentication controller for login, logout, and token management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate user and return JWT tokens.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        // Set refresh token as HTTP-only cookie
        if (!string.IsNullOrEmpty(result.Value.RefreshToken))
        {
            SetRefreshTokenCookie(result.Value.RefreshToken, result.Value.ExpiresAt.AddDays(7));
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Refresh access token using refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken(
        [FromBody] RefreshTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        // Get refresh token from cookie if not in body
        var refreshToken = request.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
        {
            refreshToken = Request.Cookies["refreshToken"];
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { error = "Refresh token is required." });
        }

        var command = new RefreshTokenCommand(refreshToken);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            // Clear cookie if refresh failed
            Response.Cookies.Delete("refreshToken");
            return Unauthorized(new { error = result.ErrorMessage });
        }

        // Set refresh token as HTTP-only cookie
        if (!string.IsNullOrEmpty(result.Value.RefreshToken))
        {
            SetRefreshTokenCookie(result.Value.RefreshToken, result.Value.ExpiresAt.AddDays(7));
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Logout user and revoke tokens.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        // Log the logout event
        if (_currentUserService.UserId.HasValue)
        {
            await _auditLogService.LogAuthenticationAsync(
                _currentUserService.UserId.Value,
                "LOGOUT",
                "User logged out",
                _currentUserService.IpAddress,
                _currentUserService.UserAgent,
                true,
                cancellationToken);
        }

        // Delete refresh token cookie
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Change user password.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new { error = "Passwords do not match." });
        }

        var command = new ChangePasswordCommand(request.CurrentPassword, request.NewPassword, request.ConfirmPassword);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Password changed successfully." });
    }

    private void SetRefreshTokenCookie(string token, DateTimeOffset expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires
        };

        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }
}
