using System.Security.Claims;
using Application.Common;
using Application.Dtos.Business;
using Application.Dtos.Employee;
using Application.Dtos.WorkingHour;
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
    private const int MaxGalleryImages = 10;
    private const long MaxGalleryRequestSize = MaxImageSize * MaxGalleryImages;

    private const string BusinessImagesFolder = "booking-saas/businesses";
    private const string BusinessGalleryFolder = "booking-saas/businesses/gallery";

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

        return CreatedAtAction(
            nameof(GetById),
            new { id = business.Id },
            ToBusinessDto(business));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BusinessDto>>> GetAll(
        [FromQuery] BusinessListQuery query)
    {
        var businesses = _dbContext.Businesses.AsNoTracking();

        var city = query.City?.Trim();
        if (!string.IsNullOrEmpty(city))
            businesses = businesses.Where(b => EF.Functions.ILike(b.City, city));

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{search}%";
            businesses = businesses.Where(b =>
                EF.Functions.ILike(b.Name, pattern) ||
                EF.Functions.ILike(b.City, pattern) ||
                EF.Functions.ILike(b.Address, pattern) ||
                (b.Description != null && EF.Functions.ILike(b.Description, pattern)));
        }

        var totalCount = await businesses.CountAsync();
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var sortBy = string.IsNullOrWhiteSpace(query.SortBy)
            ? "name"
            : query.SortBy.Trim().ToLowerInvariant();
        var sortAsc = !string.Equals(
            query.SortOrder?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

        IQueryable<Business> sorted = (sortBy, sortAsc) switch
        {
            ("city", true) => businesses.OrderBy(b => b.City).ThenBy(b => b.Id),
            ("city", false) => businesses.OrderByDescending(b => b.City).ThenByDescending(b => b.Id),
            ("id", true) => businesses.OrderBy(b => b.Id),
            ("id", false) => businesses.OrderByDescending(b => b.Id),
            ("name", false) => businesses.OrderByDescending(b => b.Name).ThenByDescending(b => b.Id),
            _ => businesses.OrderBy(b => b.Name).ThenBy(b => b.Id),
        };

        var rows = await sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        var items = rows.Select(ToBusinessDto).ToList();

        return Ok(new PagedResult<BusinessDto>(
            items, query.Page, query.PageSize, totalCount, totalPages));
    }

    /// <summary>
    /// Public business detail. Nested employees omit phone/userId.
    /// Working hours default to business-local today … +30 days; override with hoursFrom/hoursTo.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<BusinessDetailsDto>> GetById(
        int id,
        [FromQuery] DateOnly? hoursFrom = null,
        [FromQuery] DateOnly? hoursTo = null)
    {
        var business = await _dbContext.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (business is null)
            return NotFound();

        if (!TryResolveHoursWindow(
                business.TimeZoneId,
                hoursFrom,
                hoursTo,
                out var from,
                out var to,
                out var windowError))
            return BadRequest(windowError);

        var employeeRows = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => e.BusinessId == id)
            .OrderBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.User.FullName,
                AvatarUrl = e.User.ProfileImageUrl
            })
            .ToListAsync();

        var employeeIds = employeeRows.Select(e => e.Id).ToList();

        var serviceRows = employeeIds.Count == 0
            ? []
            : await _dbContext.EmployeeServices
                .AsNoTracking()
                .Where(es =>
                    employeeIds.Contains(es.EmployeesId) &&
                    es.Service.BusinessId == id)
                .OrderBy(es => es.ServicesId)
                .Select(es => new
                {
                    EmployeeId = es.EmployeesId,
                    Service = new PublicEmployeeServiceDto(
                        es.Service.Id,
                        es.Service.Title,
                        es.PriceOverride ?? es.Service.Price,
                        es.DurationMinutesOverride ?? es.Service.DurationMinutes)
                })
                .ToListAsync();

        var hourRows = employeeIds.Count == 0
            ? []
            : await _dbContext.WorkingHours
                .AsNoTracking()
                .Where(w =>
                    employeeIds.Contains(w.EmployeeId) &&
                    w.Date >= from &&
                    w.Date <= to)
                .OrderBy(w => w.Date)
                .ThenBy(w => w.StartTime)
                .Select(w => new WorkingHourDto(
                    w.Id,
                    w.EmployeeId,
                    w.Date,
                    w.DayOfWeek,
                    w.StartTime,
                    w.EndTime))
                .ToListAsync();

        var servicesByEmployee = serviceRows
            .GroupBy(s => s.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<PublicEmployeeServiceDto>)g.Select(x => x.Service).ToList());

        var hoursByEmployee = hourRows
            .GroupBy(h => h.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<WorkingHourDto>)g.ToList());

        var employees = employeeRows
            .Select(e => new PublicEmployeeDto(
                e.Id,
                e.FullName,
                e.AvatarUrl,
                servicesByEmployee.GetValueOrDefault(e.Id, Array.Empty<PublicEmployeeServiceDto>()),
                hoursByEmployee.GetValueOrDefault(e.Id, Array.Empty<WorkingHourDto>())))
            .ToList();

        return Ok(new BusinessDetailsDto(
            business.Id,
            business.Name,
            business.City,
            business.Address,
            business.Description,
            business.ProfileImageUrl,
            ToGalleryDtos(business.BusinessGallery),
            business.OwnerId,
            employees));
    }

    private const int DefaultHoursWindowDays = 30;
    private const int MaxHoursWindowDays = 90;

    private static bool TryResolveHoursWindow(
        string timeZoneId,
        DateOnly? hoursFrom,
        DateOnly? hoursTo,
        out DateOnly from,
        out DateOnly to,
        out string? error)
    {
        from = default;
        to = default;
        error = null;

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        }

        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));

        from = hoursFrom ?? today;
        to = hoursTo ?? from.AddDays(DefaultHoursWindowDays);

        if (to < from)
        {
            error = "hoursTo must be on or after hoursFrom.";
            return false;
        }

        if (to.DayNumber - from.DayNumber > MaxHoursWindowDays)
        {
            error = $"Working-hours window cannot exceed {MaxHoursWindowDays} days.";
            return false;
        }

        return true;
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<MyBusinessResponse>> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
            return Unauthorized();

        var entity = await _dbContext.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.OwnerId == userId);

        var response = new MyBusinessResponse(
            HasBusiness: entity is not null,
            Business: entity is null ? null : ToBusinessDto(entity));

        return Ok(response);
    }
    [Authorize(Roles = "BusinessOwner")]
    [HttpPut("{id}")]
    public async Task<ActionResult<BusinessDto>> Update(
        int id,
        CreateBusinessDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var business = await _dbContext.Businesses
            .FirstOrDefaultAsync(b => b.Id == id);
        if (userId != business?.OwnerId)
        {
            return Forbid();
        }
        if (business is null)
            return NotFound();

        business.Name = dto.Name;
        business.City = dto.City;
        business.Address = dto.Address;
        business.Description = dto.Description;

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Business updated successfully",
            data = ToBusinessDto(business)
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

    /// <summary>
    /// Append optional gallery images (after create/register or during edit).
    /// Form field name: <c>files</c>. Max 10 images total per business.
    /// </summary>
    [Authorize]
    [HttpPost("gallery")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxGalleryRequestSize)]
    public async Task<IActionResult> UploadBusinessGallery(
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (files is null || files.Count == 0)
            return BadRequest("لطفاً حداقل یک تصویر انتخاب کنید.");

        var business = await _dbContext.Businesses
            .FirstOrDefaultAsync(b => b.OwnerId == userId, cancellationToken);

        if (business is null)
            return NotFound("کسب‌وکار پیدا نشد.");

        var remaining = MaxGalleryImages - business.BusinessGallery.Count;
        if (remaining <= 0)
            return BadRequest($"حداکثر {MaxGalleryImages} تصویر در گالری مجاز است.");

        if (files.Count > remaining)
            return BadRequest(
                $"فقط {remaining} تصویر دیگر می‌توانید اضافه کنید (حداکثر {MaxGalleryImages}).");

        foreach (var file in files)
        {
            if (file is null || file.Length == 0)
                return BadRequest("لطفاً یک تصویر معتبر انتخاب کنید.");

            if (file.Length > MaxImageSize)
                return BadRequest("حجم هر تصویر نباید بیشتر از ۵ مگابایت باشد.");

            if (!AllowedContentTypes.Contains(file.ContentType))
                return BadRequest("فرمت تصویر باید JPG، PNG یا WebP باشد.");
        }

        var uploaded = new List<BusinessGalleryImage>(files.Count);

        try
        {
            foreach (var file in files)
            {
                var result = await _imageUploadService.UploadAsync(
                    file,
                    BusinessGalleryFolder,
                    cancellationToken);

                uploaded.Add(new BusinessGalleryImage
                {
                    Url = result.Url,
                    PublicId = result.PublicId
                });
            }

            business.BusinessGallery.AddRange(uploaded);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var image in uploaded)
                await TryDeleteUploadedImageAsync(image.PublicId, cancellationToken);

            throw;
        }

        return Ok(new
        {
            message = "تصاویر گالری با موفقیت اضافه شد.",
            businessGallery = ToGalleryDtos(business.BusinessGallery)
        });
    }

    [Authorize]
    [HttpDelete("gallery")]
    public async Task<IActionResult> DeleteBusinessGalleryImage(
        [FromQuery] string publicId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(publicId))
            return BadRequest("شناسه تصویر مشخص نشده است.");

        var business = await _dbContext.Businesses
            .FirstOrDefaultAsync(b => b.OwnerId == userId, cancellationToken);

        if (business is null)
            return NotFound("کسب‌وکار پیدا نشد.");

        var image = business.BusinessGallery
            .FirstOrDefault(g => g.PublicId == publicId);

        if (image is null)
            return NotFound("تصویر گالری پیدا نشد.");

        business.BusinessGallery.Remove(image);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _imageUploadService.DeleteAsync(publicId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete gallery image {PublicId} for business {BusinessId}",
                publicId,
                business.Id);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "تصویر از گالری حذف شد، اما حذف فایل از فضای ذخیره‌سازی ناموفق بود.");
        }

        return Ok(new
        {
            message = "تصویر گالری با موفقیت حذف شد.",
            businessGallery = ToGalleryDtos(business.BusinessGallery)
        });
    }

    private static BusinessDto ToBusinessDto(Business business) =>
        new(
            business.Id,
            business.Name,
            business.City,
            business.Address,
            business.Description,
            business.ProfileImageUrl,
            ToGalleryDtos(business.BusinessGallery),
            business.OwnerId);

    private static IReadOnlyList<BusinessGalleryImageDto> ToGalleryDtos(
        IEnumerable<BusinessGalleryImage>? gallery) =>
        (gallery ?? Array.Empty<BusinessGalleryImage>())
            .Select(g => new BusinessGalleryImageDto(g.Url, g.PublicId))
            .ToList();

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