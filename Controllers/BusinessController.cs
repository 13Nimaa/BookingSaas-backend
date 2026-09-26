using System.Security.Claims;
using Application.Dtos.Business;
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
public class BusinessController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    public BusinessController(AppDbContext dbContext,
     UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
        ITokenService tokenService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;

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
                b.OwnerId
            ))
            .FirstOrDefaultAsync();

        if (business is null)
            return NotFound();

        return Ok(business);
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<BusinessDto>>> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
            return Unauthorized();

        var businesses = await _dbContext.Businesses
            .Where(b => b.OwnerId == userId)
            .Select(b => new BusinessDto(
                b.Id,
                b.Name,
                b.City,
                b.Address,
                b.Description,
                b.OwnerId
            ))
            .ToListAsync();

        return Ok(businesses);
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