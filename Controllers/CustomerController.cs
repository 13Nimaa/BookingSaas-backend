using System.Security.Claims;
using Application.Common;
using Application.Dtos.Customer;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Route("api/business/{businessId}/customers")]
public class CustomerController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public CustomerController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Admin can manage any business; BusinessOwner only their own.
    private bool CanManageBusiness(string? ownerId) =>
        User.IsInRole("Admin") ||
        User.FindFirstValue(ClaimTypes.NameIdentifier) == ownerId;

    private static CustomerDto ToDto(Customer c) => new(
        c.Id,
        c.Name,
        c.PhoneNumber,
        c.Email,
        c.Notes,
        c.IsActive,
        c.CreatedAt,
        c.UpdatedAt,
        c.BusinessId);

    [Authorize(Roles = "Admin,BusinessOwner")]
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerDto>>> GetAll(
        int businessId, [FromQuery] CustomerListQuery query)
    {
        var business = await _dbContext.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == businessId);
        if (business is null)
            return NotFound();

        if (!CanManageBusiness(business.OwnerId))
            return Forbid();

        var customers = _dbContext.Customers
            .AsNoTracking()
            .Where(c => c.BusinessId == businessId);

        if (query.IsActive is not null)
            customers = customers.Where(c => c.IsActive == query.IsActive);

        if (query.CreatedFrom is not null)
            customers = customers.Where(c => c.CreatedAt >= query.CreatedFrom);

        if (query.CreatedTo is not null)
        {
            // Include the entire createdTo calendar day (not only midnight).
            var to = query.CreatedTo.Value;
            var createdToExclusive = new DateTime(
                to.Year, to.Month, to.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
            customers = customers.Where(c => c.CreatedAt < createdToExclusive);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            customers = customers.Where(c =>
                c.Name.Contains(search) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(search)) ||
                (c.Email != null && c.Email.Contains(search)));
        }

        var totalCount = await customers.CountAsync();
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var sortBy = string.IsNullOrWhiteSpace(query.SortBy)
            ? "createdat"
            : query.SortBy.Trim().ToLowerInvariant();
        var sortAsc = string.Equals(
            query.SortOrder?.Trim(), "asc", StringComparison.OrdinalIgnoreCase);

        IQueryable<Customer> sorted = (sortBy, sortAsc) switch
        {
            ("name", true) => customers.OrderBy(c => c.Name).ThenBy(c => c.Id),
            ("name", false) => customers.OrderByDescending(c => c.Name).ThenByDescending(c => c.Id),
            ("updatedat", true) => customers
                .OrderBy(c => c.UpdatedAt ?? DateTime.MinValue)
                .ThenBy(c => c.Id),
            ("updatedat", false) => customers
                .OrderByDescending(c => c.UpdatedAt ?? DateTime.MinValue)
                .ThenByDescending(c => c.Id),
            (_, true) => customers.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id),
            _ => customers.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id),
        };

        var items = await sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CustomerDto(
                c.Id,
                c.Name,
                c.PhoneNumber,
                c.Email,
                c.Notes,
                c.IsActive,
                c.CreatedAt,
                c.UpdatedAt,
                c.BusinessId))
            .ToListAsync();

        return Ok(new PagedResult<CustomerDto>(
            items, query.Page, query.PageSize, totalCount, totalPages));
    }

    [Authorize(Roles = "Admin,BusinessOwner")]
    [HttpGet("{customerId}")]
    public async Task<ActionResult<CustomerDto>> GetById(int businessId, int customerId)
    {
        var business = await _dbContext.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
        if (business is null)
            return NotFound();

        if (!CanManageBusiness(business.OwnerId))
            return Forbid();

        var customer = await _dbContext.Customers
            .Where(c => c.Id == customerId && c.BusinessId == businessId)
            .Select(c => new CustomerDto(
                c.Id,
                c.Name,
                c.PhoneNumber,
                c.Email,
                c.Notes,
                c.IsActive,
                c.CreatedAt,
                c.UpdatedAt,
                c.BusinessId))
            .FirstOrDefaultAsync();

        if (customer is null)
            return NotFound();

        return Ok(customer);
    }

    [Authorize(Roles = "Admin,BusinessOwner")]
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(int businessId, CreateCustomerDto dto)
    {
        var business = await _dbContext.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
        if (business is null)
            return NotFound();

        if (!CanManageBusiness(business.OwnerId))
            return Forbid();

        var customer = new Customer
        {
            Name = dto.Name,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            Notes = dto.Notes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            BusinessId = business.Id
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { businessId, customerId = customer.Id },
            ToDto(customer));
    }

    [Authorize(Roles = "Admin,BusinessOwner")]
    [HttpPut("{customerId}")]
    public async Task<ActionResult<CustomerDto>> Update(
        int businessId, int customerId, UpdateCustomerDto dto)
    {
        var business = await _dbContext.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
        if (business is null)
            return NotFound();

        if (!CanManageBusiness(business.OwnerId))
            return Forbid();

        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.BusinessId == businessId);

        if (customer is null)
            return NotFound();

        customer.Name = dto.Name;
        customer.PhoneNumber = dto.PhoneNumber;
        customer.Email = dto.Email;
        customer.Notes = dto.Notes;
        customer.IsActive = dto.IsActive;
        customer.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(ToDto(customer));
    }

    // Soft-delete: Booking→Customer is Cascade (hard delete wipes history);
    // Appointment→Customer is Restrict (hard delete fails when appointments exist).
    [Authorize(Roles = "Admin,BusinessOwner")]
    [HttpDelete("{customerId}")]
    public async Task<IActionResult> Delete(int businessId, int customerId)
    {
        var business = await _dbContext.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
        if (business is null)
            return NotFound();

        if (!CanManageBusiness(business.OwnerId))
            return Forbid();

        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId && c.BusinessId == businessId);

        if (customer is null)
            return NotFound();

        customer.IsActive = false;
        customer.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }
}
