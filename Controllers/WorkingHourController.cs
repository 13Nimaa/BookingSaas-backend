using System.Security.Claims;
using Application.Dtos.WorkingHour;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Route("api/business/{businessId}/employees/{employeeId}/working-hours")]
[Authorize(Roles = "Employee")]
public class WorkingHourController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public WorkingHourController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<ActionResult<WorkingHourDto>> Create(
        int businessId,
        int employeeId,
        CreateWorkingHourDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
            return Unauthorized("کاربر احراز هویت نشده است.");

        var employee = await _dbContext.Employees
            .FirstOrDefaultAsync(e =>
                e.Id == employeeId &&
                e.BusinessId == businessId &&
                e.UserId == userId);

        if (employee is null)
            return NotFound("کارمند موردنظر پیدا نشد.");

        if (dto.EndTime <= dto.StartTime)
            return BadRequest("ساعت پایان باید بعد از ساعت شروع باشد.");

        var hasOverlap = await _dbContext.WorkingHours
            .AnyAsync(w =>
                w.EmployeeId == employeeId &&
                w.DayOfWeek == dto.DayOfWeek &&
                w.StartTime < dto.EndTime &&
                w.EndTime > dto.StartTime);

        if (hasOverlap)
            return Conflict("این بازه زمانی با یکی از ساعات کاری موجود تداخل دارد.");

        var workingHour = new WorkingHour
        {
            EmployeeId = employeeId,
            DayOfWeek = dto.DayOfWeek,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime
        };

        _dbContext.WorkingHours.Add(workingHour);
        await _dbContext.SaveChangesAsync();

        var result = new WorkingHourDto(
            workingHour.Id,
            workingHour.EmployeeId,
            workingHour.DayOfWeek,
            workingHour.StartTime,
            workingHour.EndTime);

        return CreatedAtAction(
            nameof(GetAll),
            new
            {
                businessId,
                employeeId
            },
            new
            {
                message = "ساعت کاری با موفقیت ثبت شد.",
                data = result
            });
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkingHourDto>>> GetAll(
        int businessId,
        int employeeId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
            return Unauthorized("کاربر احراز هویت نشده است.");

        var employeeExists = await _dbContext.Employees
            .AnyAsync(e =>
                e.Id == employeeId &&
                e.BusinessId == businessId &&
                e.UserId == userId);

        if (!employeeExists)
            return NotFound("کارمند موردنظر پیدا نشد.");

        var workingHours = await _dbContext.WorkingHours
            .Where(w => w.EmployeeId == employeeId)
            .OrderBy(w => w.DayOfWeek)
            .ThenBy(w => w.StartTime)
            .Select(w => new WorkingHourDto(
                w.Id,
                w.EmployeeId,
                w.DayOfWeek,
                w.StartTime,
                w.EndTime))
            .ToListAsync();

        return Ok(workingHours);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<WorkingHourDto>> Update(
        int businessId,
        int employeeId,
        int id,
        CreateWorkingHourDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
            return Unauthorized("کاربر احراز هویت نشده است.");

        var employeeExists = await _dbContext.Employees
            .AnyAsync(e =>
                e.Id == employeeId &&
                e.BusinessId == businessId &&
                e.UserId == userId);

        if (!employeeExists)
            return NotFound("کارمند موردنظر پیدا نشد.");

        var workingHour = await _dbContext.WorkingHours
            .FirstOrDefaultAsync(w =>
                w.Id == id &&
                w.EmployeeId == employeeId);

        if (workingHour is null)
            return NotFound("ساعت کاری موردنظر پیدا نشد.");

        if (dto.EndTime <= dto.StartTime)
            return BadRequest("ساعت پایان باید بعد از ساعت شروع باشد.");

        var hasOverlap = await _dbContext.WorkingHours
            .AnyAsync(w =>
                w.Id != id &&
                w.EmployeeId == employeeId &&
                w.DayOfWeek == dto.DayOfWeek &&
                w.StartTime < dto.EndTime &&
                w.EndTime > dto.StartTime);

        if (hasOverlap)
            return Conflict("این بازه زمانی با یکی از ساعات کاری موجود تداخل دارد.");

        workingHour.DayOfWeek = dto.DayOfWeek;
        workingHour.StartTime = dto.StartTime;
        workingHour.EndTime = dto.EndTime;

        await _dbContext.SaveChangesAsync();

        var result = new WorkingHourDto(
            workingHour.Id,
            workingHour.EmployeeId,
            workingHour.DayOfWeek,
            workingHour.StartTime,
            workingHour.EndTime);

        return Ok(new
        {
            message = "ساعت کاری با موفقیت ویرایش شد.",
            data = result
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(
        int businessId,
        int employeeId,
        int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null)
            return Unauthorized("کاربر احراز هویت نشده است.");

        var employeeExists = await _dbContext.Employees
            .AnyAsync(e =>
                e.Id == employeeId &&
                e.BusinessId == businessId &&
                e.UserId == userId);

        if (!employeeExists)
            return NotFound("کارمند موردنظر پیدا نشد.");

        var workingHour = await _dbContext.WorkingHours
            .FirstOrDefaultAsync(w =>
                w.Id == id &&
                w.EmployeeId == employeeId);

        if (workingHour is null)
            return NotFound("ساعت کاری موردنظر پیدا نشد.");

        _dbContext.WorkingHours.Remove(workingHour);

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "ساعت کاری با موفقیت حذف شد."
        });
    }
}