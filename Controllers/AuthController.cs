
using System.Security.Claims;
using Application.Interfaces;
using BookingSaas_backend.Application.Dtos.AuthDtos;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly AppDbContext _dbContext;


    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
            AppDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _dbContext = dbContext;

    }
    private async Task<AuthResponseDto> BuildAutResponseDto(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Token = _tokenService.HashRefreshToken(rawRefreshToken),
            UserId = user.Id,
            User = user,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        });
        await _dbContext.SaveChangesAsync();

        Response.Cookies.Append("refreshToken", rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return new AuthResponseDto(
                 new UserDto(user.Id, user.FullName, user.Email!),
                 accessToken,
                 rawRefreshToken,
                 DateTimeOffset.UtcNow.AddMinutes(15)
             );
    }
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(SignupDto dto)
    {
        if (dto.Password != dto.ConfirmPassword)
            return BadRequest("Passwords do not match.");

        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser is not null)
            return Conflict("Email is already registered.");

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.Name
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        const string defaultRole = "Customer";
        if (!await _roleManager.RoleExistsAsync(defaultRole))
            await _roleManager.CreateAsync(new IdentityRole(defaultRole));

        await _userManager.AddToRoleAsync(user, defaultRole);

        var response = await BuildAutResponseDto(user);

        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            return Unauthorized("Invalid email or password.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!passwordValid)
            return Unauthorized("Invalid email or password.");


        var response = await BuildAutResponseDto(user);


        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh()
    {
        var rawToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized();

        var hashedToken = _tokenService.HashRefreshToken(rawToken);

        var existingToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == hashedToken);

        if (existingToken is null || !existingToken.IsActive)
            return Unauthorized();

        existingToken.RevokedAt = DateTimeOffset.UtcNow;

        var response = await BuildAutResponseDto(existingToken.User);
        return Ok(response);
    }
    [HttpPost("logout")]
    public async Task<ActionResult> LogOut()
    {
        var rawToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(rawToken))
        {
            var hashedToken = _tokenService.HashRefreshToken(rawToken);
            var existingToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == hashedToken);
            if (existingToken is not null && existingToken.IsActive)
            {
                existingToken.RevokedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }

        Response.Cookies.Delete("refreshToken");
        return Ok(new { Message = "Logged out successfully." });
    }

    [HttpGet("session")]
    [Authorize]
    public async Task<ActionResult> Session()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user is null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            Authenticated = true,
            User = new UserDto(user.Id, user.FullName, user.Email!),
            Roles = roles
        });
    }
}
