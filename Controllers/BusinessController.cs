using System.Security.Claims;
using Application.Dtos.Business;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BusinessController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public BusinessController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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
}