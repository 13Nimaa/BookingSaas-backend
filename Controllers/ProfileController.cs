
using System.Security.Claims;
using Application.Dtos.Profile;
using BookingSaas_backend.Application.Interfaces;
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
    private const long MaxImageSize = 5 * 1024 * 1024;

    private const string ProfileImagesFolder = "booking-saas/profiles";

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImageUploadService _imageUploadService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        IImageUploadService imageUploadService,
        ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _imageUploadService = imageUploadService;
        _logger = logger;
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

        return Ok(await MapProfileAsync(user));
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
            !string.Equals(
                dto.Email.Trim(),
                user.Email,
                StringComparison.OrdinalIgnoreCase))
        {
            var normalizedEmail = dto.Email.Trim();

            var existingUser = await _userManager.FindByEmailAsync(
                normalizedEmail);

            if (existingUser is not null && existingUser.Id != user.Id)
                return Conflict("این ایمیل قبلاً استفاده شده است.");

            user.Email = normalizedEmail;
        }

        user.FullName = dto.FullName.Trim();

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(
                updateResult.Errors.Select(e => e.Description));
        }

        return Ok(await MapProfileAsync(user));
    }

    [HttpPost("image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImageSize)]
    public async Task<IActionResult> UploadProfileImage(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (file is null || file.Length == 0)
            return BadRequest("لطفاً یک تصویر انتخاب کنید.");

        if (file.Length > MaxImageSize)
            return BadRequest("حجم تصویر نباید بیشتر از ۵ مگابایت باشد.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest("فرمت تصویر باید JPG، PNG یا WebP باشد.");

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
            return NotFound("کاربر پیدا نشد.");

        var uploadedImage = await _imageUploadService.UploadAsync(
            file,
            ProfileImagesFolder,
            cancellationToken);

        var oldPublicId = user.ProfileImagePublicId;

        user.ProfileImageUrl = uploadedImage.Url;
        user.ProfileImagePublicId = uploadedImage.PublicId;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            await TryDeleteUploadedImageAsync(
                uploadedImage.PublicId,
                cancellationToken);

            return BadRequest(
                updateResult.Errors.Select(e => e.Description));
        }

        if (!string.IsNullOrWhiteSpace(oldPublicId))
        {
            await TryDeleteUploadedImageAsync(
                oldPublicId,
                cancellationToken);
        }

        return Ok(new
        {
            message = "تصویر پروفایل با موفقیت به‌روزرسانی شد.",
            profileImageUrl = user.ProfileImageUrl
        });
    }

    [HttpDelete("image")]
    public async Task<IActionResult> DeleteProfileImage(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
            return NotFound("کاربر پیدا نشد.");

        if (string.IsNullOrWhiteSpace(user.ProfileImagePublicId))
            return NotFound("کاربر تصویر پروفایل ندارد.");

        var oldPublicId = user.ProfileImagePublicId;

        user.ProfileImageUrl = null;
        user.ProfileImagePublicId = null;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            return BadRequest(
                updateResult.Errors.Select(e => e.Description));
        }

        try
        {
            await _imageUploadService.DeleteAsync(
                oldPublicId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete profile image {PublicId} for user {UserId}",
                oldPublicId,
                userId);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "عکس از پروفایل حذف شد، اما حذف فایل از فضای ذخیره‌سازی ناموفق بود.");
        }

        return Ok(new
        {
            message = "تصویر پروفایل با موفقیت حذف شد."
        });
    }

    private async Task<UserProfileDto> MapProfileAsync(
        ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserProfileDto(
            user.Id,
            user.FullName,
            user.PhoneNumber,
            user.ProfileImageUrl,
            user.Email,
            roles,
            user.OwnedBusiness?.Id,
            user.EmployeeProfile?.Id
        );
    }

    private async Task TryDeleteUploadedImageAsync(
        string publicId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _imageUploadService.DeleteAsync(
                publicId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to clean up Cloudinary image {PublicId}",
                publicId);
        }
    }
}
