using System.Security.Claims;
using Application.Dtos.Service;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Route("api/business/{businessId}/services")]
public class ServiceController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    public ServiceController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Admin can manage any business; BusinessOwner only their own.
    private bool CanManageBusiness(string? ownerId) =>
        User.IsInRole("Admin") ||
        User.FindFirstValue(ClaimTypes.NameIdentifier) == ownerId;

    [Authorize(Roles = "Admin,BusinessOwner")]
    [HttpPost]
    public async Task<ActionResult<ServiceDto>> Create(int businessId, CreateServiceDto dto)
    {
        var business = await _dbContext.Businesses.Where(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null)
            return NotFound();
        if (!CanManageBusiness(business.OwnerId))
            return Forbid();
        var service = new Service
        {
            Title = dto.Title,
            Price = dto.Price,
            DurationMinutes = dto.DurationMinutes,
            BusinessId = businessId
        };
        _dbContext.Services.Add(service);
        await _dbContext.SaveChangesAsync();
        var result = new ServiceDto(service.Id, service.Title, service.Price, service.DurationMinutes, service.BusinessId);

        return CreatedAtAction(nameof(GetById), new { businessId, serviceId = service.Id }, result);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<ServiceDto>>> GetAll(int businessId)
    {
        var services = await _dbContext.Services.Where(b => b.BusinessId == businessId).Select(s => new ServiceDto(s.Id, s.Title, s.Price, s.DurationMinutes, s.BusinessId)).ToListAsync();

        return Ok(services);
    }

    [Authorize(Roles = "Admin,BusinessOwner")]
    [HttpGet("{serviceId}")]
    public async Task<ActionResult<ServiceDto>> GetById(int businessId, int serviceId)
    {
        var business = await _dbContext.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
        if (business is null)
            return NotFound();

        if (!CanManageBusiness(business.OwnerId))
            return Forbid();

        var service = await _dbContext.Services
            .Where(s => s.Id == serviceId && s.BusinessId == businessId)
            .Select(s => new ServiceDto(s.Id, s.Title, s.Price, s.DurationMinutes, s.BusinessId))
            .FirstOrDefaultAsync();

        if (service is null)
            return NotFound();

        return Ok(service);
    }

    [Authorize(Roles = "Admin,BusinessOwner")]
    [HttpPut("{serviceId}")]
    public async Task<ActionResult<ServiceDto>> Update(int businessId, int serviceId, UpdateServiceDto dto)
    {
        var business = await _dbContext.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
        if (business is null)
            return NotFound();

        if (!CanManageBusiness(business.OwnerId))
            return Forbid();

        var service = await _dbContext.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.BusinessId == businessId);

        if (service is null)
            return NotFound();

        service.Title = dto.Title;
        service.Price = dto.Price;
        service.DurationMinutes = dto.DurationMinutes;

        await _dbContext.SaveChangesAsync();

        var result = new ServiceDto(service.Id, service.Title, service.Price, service.DurationMinutes, service.BusinessId);
        return Ok(result);
    }

    [Authorize(Roles = "Admin,BusinessOwner")]
    [HttpDelete("{serviceId}")]
    public async Task<IActionResult> Delete(int businessId, int serviceId)
    {
        var business = await _dbContext.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
        if (business is null)
            return NotFound();

        if (!CanManageBusiness(business.OwnerId))
            return Forbid();

        var service = await _dbContext.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.BusinessId == businessId);

        if (service is null)
            return NotFound();

        _dbContext.Services.Remove(service);
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "خدمت با موفقیت حذف شد."
        });
    }
}
