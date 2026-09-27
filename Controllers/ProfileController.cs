using System.Security.Claims;
using Application.Dtos.Profile;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Route("profile")]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _dbContext;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
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
            return NotFound("User not found.");

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
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest("FullName is required.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.Users
            .Include(u => u.OwnedBusiness)
            .Include(u => u.EmployeeProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return NotFound("User not found.");

        // If phone number is being changed, ensure it's not taken by another user
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && dto.PhoneNumber != user.PhoneNumber)
        {
            var existingUserWithPhone = await _userManager.Users
                .FirstOrDefaultAsync(u => (u.PhoneNumber == dto.PhoneNumber || u.UserName == dto.PhoneNumber) && u.Id != userId);

            if (existingUserWithPhone is not null)
                return Conflict("Phone number is already in use by another account.");

            user.PhoneNumber = dto.PhoneNumber;
            // Since username is the phone number in this system:
            user.UserName = dto.PhoneNumber;
        }

        // If email is being changed, check if email is unique if provided
        if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
        {
            var existingUserWithEmail = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.Id != userId);

            if (existingUserWithEmail is not null)
                return Conflict("Email is already in use by another account.");

            user.Email = dto.Email;
        }

        user.FullName = dto.FullName.Trim();

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return BadRequest(updateResult.Errors.Select(e => e.Description));

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
