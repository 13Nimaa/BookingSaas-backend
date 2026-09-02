using System.Security.Claims;
using Application.Dtos.Business;
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
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ServiceDto>> Create(int businessId, CreateServiceDto dto)
    {
        var business = await _dbContext.Businesses.Where(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null)
            return NotFound();
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) != business.OwnerId)
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

        return CreatedAtAction(nameof(Create), new { businessId }, result);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<ServiceDto>>> GetAll(int businessId)
    {
        var services = await _dbContext.Services.Where(b => b.BusinessId == businessId).Select(s => new ServiceDto(s.Id, s.Title, s.Price, s.DurationMinutes, s.BusinessId)).ToListAsync();

        return Ok(services);
    }
}
