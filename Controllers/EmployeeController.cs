using System.Security.Claims;
using Application.Dtos.Employee;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Route("api/business/{businessId}/employees")]
public class EmployeeController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public EmployeeController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [Authorize(Roles = "BusinessOwner")]
    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(
        int businessId,
        CreateEmployeeDto dto)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (ownerId is null)
            return Unauthorized();

        var business = await _dbContext.Businesses
            .FirstOrDefaultAsync(b =>
                b.Id == businessId &&
                b.OwnerId == ownerId);

        if (business is null)
            return NotFound("Business not found.");

        var user = await _userManager.FindByNameAsync(dto.PhoneNumber);

        if (user is null)
            return NotFound("User with this phone number does not exist.");

        var isCustomer = await _userManager.IsInRoleAsync(user, "Customer");

        if (!isCustomer)
            return Conflict("This user cannot be added as an employee.");

        var existingEmployee = await _dbContext.Employees
            .FirstOrDefaultAsync(e => e.UserId == user.Id);

        if (existingEmployee is not null)
            return Conflict("This user is already an employee.");

        const string employeeRole = "Employee";

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync();

        try
        {
            if (!await _roleManager.RoleExistsAsync(employeeRole))
            {
                var roleResult = await _roleManager.CreateAsync(
                    new IdentityRole(employeeRole));

                if (!roleResult.Succeeded)
                    return BadRequest(
                        roleResult.Errors.Select(e => e.Description));
            }

            var employee = new Employee
            {
                UserId = user.Id,
                BusinessId = businessId
            };

            _dbContext.Employees.Add(employee);

            await _dbContext.SaveChangesAsync();

            var removeCustomerResult =
                await _userManager.RemoveFromRoleAsync(user, "Customer");

            if (!removeCustomerResult.Succeeded)
            {
                await transaction.RollbackAsync();

                return BadRequest(
                    removeCustomerResult.Errors.Select(e => e.Description));
            }

            var addEmployeeResult =
                await _userManager.AddToRoleAsync(user, employeeRole);

            if (!addEmployeeResult.Succeeded)
            {
                await transaction.RollbackAsync();

                return BadRequest(
                    addEmployeeResult.Errors.Select(e => e.Description));
            }

            await transaction.CommitAsync();

            var result = new EmployeeDto(
                employee.Id,
                user.Id,
                user.FullName,
                user.PhoneNumber ?? string.Empty,
                employee.BusinessId);

            return CreatedAtAction(
                nameof(Create),
                new { businessId },
                result);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}