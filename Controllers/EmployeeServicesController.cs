using System.Security.Claims;
using Application.Dtos.Employee;
using Application.Dtos.Service;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Authorize(Roles = "BusinessOwner")]
[Route("api/employees/{employeeId:int}/services")]
public class EmployeeServicesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public EmployeeServicesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<ServiceDto>>> GetAssignedServices(int employeeId)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
            return Unauthorized();

        var employee = await _dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee is null)
            return NotFound("کارمند یافت نشد.");

        if (!await OwnsBusinessAsync(employee.BusinessId, ownerId))
            return Forbid();

        var services = await _dbContext.Employees
            .Where(e => e.Id == employeeId)
            .SelectMany(e => e.Services)
            .Where(s => s.BusinessId == employee.BusinessId)
            .OrderBy(s => s.Id)
            .Select(s => new ServiceDto(
                s.Id,
                s.Title,
                s.Price,
                s.DurationMinutes,
                s.BusinessId))
            .ToListAsync();

        return Ok(services);
    }

    [HttpPost]
    public async Task<ActionResult<ServiceDto>> AssignService(
        int employeeId,
        AssignServiceDto dto)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
            return Unauthorized();

        var employee = await _dbContext.Employees
            .Include(e => e.Services)
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee is null)
            return NotFound("کارمند یافت نشد.");

        if (!await OwnsBusinessAsync(employee.BusinessId, ownerId))
            return Forbid();

        var service = await _dbContext.Services
            .FirstOrDefaultAsync(s => s.Id == dto.ServiceId);

        if (service is null)
            return NotFound("خدمت یافت نشد.");

        if (service.BusinessId != employee.BusinessId)
            return Conflict("این خدمت متعلق به کسب‌وکار دیگری است.");

        if (employee.Services.Any(s => s.Id == service.Id))
            return Conflict("این خدمت قبلاً به این کارمند اختصاص داده شده است.");

        employee.Services.Add(service);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict("این خدمت قبلاً به این کارمند اختصاص داده شده است.");
        }

        var result = new ServiceDto(
            service.Id,
            service.Title,
            service.Price,
            service.DurationMinutes,
            service.BusinessId);

        return CreatedAtAction(
            nameof(GetAssignedServices),
            new { employeeId },
            result);
    }

    [HttpDelete("{serviceId:int}")]
    public async Task<IActionResult> RemoveService(int employeeId, int serviceId)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
            return Unauthorized();

        var employee = await _dbContext.Employees
            .Include(e => e.Services)
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee is null)
            return NotFound("کارمند یافت نشد.");

        if (!await OwnsBusinessAsync(employee.BusinessId, ownerId))
            return Forbid();

        var service = employee.Services.FirstOrDefault(s => s.Id == serviceId);
        if (service is null)
            return NotFound("این تخصیص وجود ندارد.");

        if (service.BusinessId != employee.BusinessId)
            return Conflict("این خدمت متعلق به کسب‌وکار دیگری است.");

        var hasBookings = await _dbContext.Bookings.AnyAsync(b =>
            b.EmployeeId == employeeId &&
            b.ServiceId == serviceId);

        if (hasBookings)
            return Conflict("به دلیل وجود رزرو مرتبط، امکان حذف تخصیص وجود ندارد.");

        employee.Services.Remove(service);
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "تخصیص خدمت با موفقیت حذف شد."
        });
    }

    private Task<bool> OwnsBusinessAsync(int businessId, string ownerId) =>
        _dbContext.Businesses.AnyAsync(b =>
            b.Id == businessId &&
            b.OwnerId == ownerId);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
