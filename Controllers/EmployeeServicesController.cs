using System.Security.Claims;
using Application.Dtos.Employee;
using Domain.Entities;
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
    public async Task<ActionResult<List<EmployeeServiceDto>>> GetAssignedServices(int employeeId)
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

        var services = await _dbContext.EmployeeServices
            .AsNoTracking()
            .Where(es =>
                es.EmployeesId == employeeId &&
                es.Service.BusinessId == employee.BusinessId)
            .OrderBy(es => es.ServicesId)
            .Select(es => new EmployeeServiceDto(
                es.Service.Id,
                es.Service.Title,
                es.Service.Price,
                es.Service.DurationMinutes,
                es.PriceOverride,
                es.DurationMinutesOverride,
                es.PriceOverride ?? es.Service.Price,
                es.DurationMinutesOverride ?? es.Service.DurationMinutes,
                es.Service.BusinessId))
            .ToListAsync();

        return Ok(services);
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeServiceDto>> AssignService(
        int employeeId,
        AssignServiceDto dto)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
            return Unauthorized();

        var employee = await _dbContext.Employees
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

        var alreadyAssigned = await _dbContext.EmployeeServices.AnyAsync(es =>
            es.EmployeesId == employeeId &&
            es.ServicesId == service.Id);

        if (alreadyAssigned)
            return Conflict("این خدمت قبلاً به این کارمند اختصاص داده شده است.");

        var assignment = new EmployeeService
        {
            EmployeesId = employee.Id,
            ServicesId = service.Id,
            PriceOverride = dto.PriceOverride,
            DurationMinutesOverride = dto.DurationMinutesOverride,
            Service = service,
            Employee = employee
        };

        _dbContext.EmployeeServices.Add(assignment);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return Conflict("این خدمت قبلاً به این کارمند اختصاص داده شده است.");
        }

        return CreatedAtAction(
            nameof(GetAssignedServices),
            new { employeeId },
            ToDto(assignment));
    }

    [HttpPut("{serviceId:int}")]
    public async Task<ActionResult<EmployeeServiceDto>> UpdateAssignment(
        int employeeId,
        int serviceId,
        UpdateEmployeeServiceDto dto)
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

        var assignment = await _dbContext.EmployeeServices
            .Include(es => es.Service)
            .FirstOrDefaultAsync(es =>
                es.EmployeesId == employeeId &&
                es.ServicesId == serviceId);

        if (assignment is null)
            return NotFound("این تخصیص وجود ندارد.");

        if (assignment.Service.BusinessId != employee.BusinessId)
            return Conflict("این خدمت متعلق به کسب‌وکار دیگری است.");

        if (dto.PriceOverrideSpecified)
            assignment.PriceOverride = dto.PriceOverride;

        if (dto.DurationMinutesOverrideSpecified)
            assignment.DurationMinutesOverride = dto.DurationMinutesOverride;

        await _dbContext.SaveChangesAsync();

        return Ok(ToDto(assignment));
    }

    [HttpDelete("{serviceId:int}")]
    public async Task<IActionResult> RemoveService(int employeeId, int serviceId)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
            return Unauthorized();

        var employee = await _dbContext.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee is null)
            return NotFound("کارمند یافت نشد.");

        if (!await OwnsBusinessAsync(employee.BusinessId, ownerId))
            return Forbid();

        var assignment = await _dbContext.EmployeeServices
            .Include(es => es.Service)
            .FirstOrDefaultAsync(es =>
                es.EmployeesId == employeeId &&
                es.ServicesId == serviceId);

        if (assignment is null)
            return NotFound("این تخصیص وجود ندارد.");

        if (assignment.Service.BusinessId != employee.BusinessId)
            return Conflict("این خدمت متعلق به کسب‌وکار دیگری است.");

        var hasBookings = await _dbContext.Bookings.AnyAsync(b =>
            b.EmployeeId == employeeId &&
            b.ServiceId == serviceId);

        if (hasBookings)
            return Conflict("به دلیل وجود رزرو مرتبط، امکان حذف تخصیص وجود ندارد.");

        _dbContext.EmployeeServices.Remove(assignment);
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

    private static EmployeeServiceDto ToDto(EmployeeService assignment) =>
        new(
            assignment.Service.Id,
            assignment.Service.Title,
            assignment.Service.Price,
            assignment.Service.DurationMinutes,
            assignment.PriceOverride,
            assignment.DurationMinutesOverride,
            assignment.PriceOverride ?? assignment.Service.Price,
            assignment.DurationMinutesOverride ?? assignment.Service.DurationMinutes,
            assignment.Service.BusinessId);
}
