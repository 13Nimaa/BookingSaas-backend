using System.Security.Claims;
using Application.Dtos.Business;
using Application.Interfaces;
using BookingSaas_backend.Application.Dtos.AuthDtos;
using BookingSaas_backend.Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BusinessController : ControllerBase
{
    private const long MaxImageSize = 5 * 1024 * 1024;

    private const string BusinessImagesFolder = "booking-saas/businesses";

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly IImageUploadService _imageUploadService;
    private readonly ILogger<BusinessController> _logger;
    public BusinessController(AppDbContext dbContext,
     UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        IImageUploadService imageUploadService,
        ILogger<BusinessController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _imageUploadService = imageUploadService;
        _logger = logger;

    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<BusinessDto>> Create(CreateBusinessDto dto)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (ownerId is null)
            return Unauthorized();

        var business = new Business
        {
            Name = dto.Name,
            City = dto.City,
            Address = dto.Address,
            Description = dto.Description,
            OwnerId = ownerId
        };

        _dbContext.Businesses.Add(business);
        await _dbContext.SaveChangesAsync();

        var result = new BusinessDto(
            business.Id,
            business.Name,
            business.City,
            business.Address,
            business.Description,
            business.ProfileImageUrl,
            business.OwnerId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = business.Id },
            result);
    }

    [HttpGet]
    public async Task<ActionResult<List<BusinessDto>>> GetAll()
    {
        var businesses = await _dbContext.Businesses
            .Select(b => new BusinessDto(
                b.Id,
                b.Name,
                b.City,
                b.Address,
                b.Description,
                b.ProfileImageUrl,
                b.OwnerId
            ))
            .ToListAsync();

        return Ok(businesses);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BusinessDto>> GetById(int id)
    {
        var business = await _dbContext.Businesses
            .Where(b => b.Id == id)
            .Select(b => new BusinessDto(
                b.Id,
                b.Name,
                b.City,
                b.Address,
                b.Description,
                b.ProfileImageUrl,
                b.OwnerId
            ))
            .FirstOrDefaultAsync();

        if (business is null)
            return NotFound();

        return Ok(business);
    }

[Authorize]
[HttpGet("mine")]
public async Task<ActionResult<MyBusinessResponse>> GetMine()
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (userId is null)
        return Unauthorized();

    var business = await _dbContext.Businesses
        .Where(b => b.OwnerId == userId)
        .Select(b => new BusinessDto(
            b.Id,
            b.Name,
            b.City,
            b.Address,
            b.Description,
            b.ProfileImageUrl,
            b.OwnerId
        ))
        .FirstOrDefaultAsync();

    var response = new MyBusinessResponse(
        HasBusiness: business is not null,
        Business: business
    );

    return Ok(response);
}
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<BusinessDto>> Update(
        int id,
        CreateBusinessDto dto)
    {
        var business = await _dbContext.Businesses
            .FirstOrDefaultAsync(b => b.Id == id);

        if (business is null)
            return NotFound();

        business.Name = dto.Name;
        business.City = dto.City;
        business.Address = dto.Address;
        business.Description = dto.Description;

        await _dbContext.SaveChangesAsync();

        var result = new BusinessDto(
            business.Id,
            business.Name,
            business.City,
            business.Address,
            business.Description,
            business.ProfileImageUrl,
            business.OwnerId
        );

        return Ok(new
        {
            message = "Business updated successfully",
            data = result
        });
    }
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var business = await _dbContext.Businesses
            .FirstOrDefaultAsync(b => b.Id == id);

        if (business is null)
            return NotFound();

        _dbContext.Businesses.Remove(business);

        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [Authorize]
    [HttpPost("image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImageSize)]
    public async Task<IActionResult> UploadBusinessImage(
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

        var business = await _dbContext.Businesses
            .FirstOrDefaultAsync(b => b.OwnerId == userId, cancellationToken);

        if (business is null)
            return NotFound("کسب‌وکار پیدا نشد.");

        var uploadedImage = await _imageUploadService.UploadAsync(
            file,
            BusinessImagesFolder,
            cancellationToken);

        var oldPublicId = business.ProfileImagePublicId;

        business.ProfileImageUrl = uploadedImage.Url;
        business.ProfileImagePublicId = uploadedImage.PublicId;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteUploadedImageAsync(
                uploadedImage.PublicId,
                cancellationToken);

            throw;
        }

        if (!string.IsNullOrWhiteSpace(oldPublicId))
        {
            await TryDeleteUploadedImageAsync(
                oldPublicId,
                cancellationToken);
        }

        return Ok(new
        {
            message = "تصویر کسب‌وکار با موفقیت به‌روزرسانی شد.",
            profileImageUrl = business.ProfileImageUrl
        });
    }

    [Authorize]
    [HttpDelete("image")]
    public async Task<IActionResult> DeleteBusinessImage(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var business = await _dbContext.Businesses
            .FirstOrDefaultAsync(b => b.OwnerId == userId, cancellationToken);

        if (business is null)
            return NotFound("کسب‌وکار پیدا نشد.");

        if (string.IsNullOrWhiteSpace(business.ProfileImagePublicId))
            return NotFound("کسب‌وکار تصویر پروفایل ندارد.");

        var oldPublicId = business.ProfileImagePublicId;

        business.ProfileImageUrl = null;
        business.ProfileImagePublicId = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

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
                "Failed to delete business image {PublicId} for business {BusinessId}",
                oldPublicId,
                business.Id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "عکس از کسب‌وکار حذف شد، اما حذف فایل از فضای ذخیره‌سازی ناموفق بود.");
        }

        return Ok(new
        {
            message = "تصویر کسب‌وکار با موفقیت حذف شد."
        });
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

    [Authorize]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> RegisterBusiness(
    CreateBusinessDto dto)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (ownerId is null)
            return Unauthorized();

        var alreadyExists = await _dbContext.Businesses
            .AnyAsync(b => b.OwnerId == ownerId);

        if (alreadyExists)
            return Conflict("You already have a business.");

        var user = await _userManager.FindByIdAsync(ownerId);

        if (user is null)
            return Unauthorized();

        var business = new Business
        {
            Name = dto.Name,
            City = dto.City,
            Address = dto.Address,
            Description = dto.Description,
            OwnerId = ownerId
        };

        _dbContext.Businesses.Add(business);

        const string businessOwnerRole = "BusinessOwner";

        if (!await _roleManager.RoleExistsAsync(businessOwnerRole))
        {
            var roleResult = await _roleManager.CreateAsync(
                new IdentityRole(businessOwnerRole));

            if (!roleResult.Succeeded)
                return BadRequest(
                    roleResult.Errors.Select(e => e.Description));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(
            user,
            businessOwnerRole);

        if (!addRoleResult.Succeeded)
            return BadRequest(
                addRoleResult.Errors.Select(e => e.Description));

        await _dbContext.SaveChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _tokenService.GenerateAccessToken(
            user,
            roles);

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

        var response = new AuthResponseDto(
            new UserDto(
                user.Id,
                user.FullName,
                user.PhoneNumber ?? string.Empty),
            accessToken,
            rawRefreshToken,
            DateTimeOffset.UtcNow.AddMinutes(15)
        );

        return Ok(response);
    }
}