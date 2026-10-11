using System.Security.Claims;
using Application.Dtos.Profile;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<UserProfileDto>> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.Users
            .Include(u => u.OwnedBusiness)
            .Include(u => u.EmployeeProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return NotFound("کاربر پیدا نشد.");

        var roles = await _userManager.GetRolesAsync(user);

        var profileDto = new UserProfileDto(
            user.Id,
            user.FullName,
            user.PhoneNumber,
            user.Email,
            roles,
            user.OwnedBusiness?.Id,
            user.EmployeeProfile?.Id
        );

        return Ok(profileDto);
    }

    [HttpPut]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile(
        [FromBody] UpdateProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("نام و نام خانوادگی الزامی است.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.Users
            .Include(u => u.OwnedBusiness)
            .Include(u => u.EmployeeProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return NotFound("کاربر پیدا نشد.");

        if (!string.IsNullOrWhiteSpace(dto.Email) &&
            dto.Email != user.Email)
        {
            var existingUserWithEmail = await _userManager.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == dto.Email &&
                    u.Id != userId);

            if (existingUserWithEmail is not null)
                return Conflict("این ایمیل قبلاً استفاده شده است.");

            user.Email = dto.Email;
        }

        user.FullName = dto.FullName.Trim();

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(
                updateResult.Errors.Select(e => e.Description)
            );
        }

        var roles = await _userManager.GetRolesAsync(user);

        var profileDto = new UserProfileDto(
            user.Id,
            user.FullName,
            user.PhoneNumber,
            user.Email,
            roles,
            user.OwnedBusiness?.Id,
            user.EmployeeProfile?.Id
        );

        return Ok(profileDto);
    }
}