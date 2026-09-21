using System.Security.Claims;
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
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(
        int businessId, CreateCustomerDto dto
    )
    {
        var business = await _dbContext.Businesses.Where(b => b.Id == businessId).FirstOrDefaultAsync();
        if (business is null)
        {
            return NotFound();
        }
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) != business.OwnerId)
        {
            return Forbid();
        }

        var customer = new Customer
        {
            Name = dto.Name,
            PhoneNumber = dto.PhoneNumber,
            Notes = dto.Notes,
            BusinessId = business.Id
        };
        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();
        var result = new CustomerDto(
      customer.Id,
      customer.Name,
      customer.PhoneNumber,
      customer.Notes,
      customer.BusinessId
  );
        return CreatedAtAction(nameof(Create), new { businessId }, result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> GetAll(int businessId)
    {
        var business = await _dbContext.Businesses.FirstOrDefaultAsync(b => b.Id == businessId);
        if (business is null)
            return NotFound();

        if (User.FindFirstValue(ClaimTypes.NameIdentifier) != business.OwnerId)
            return Forbid();

        var customers = await _dbContext.Customers
            .Where(c => c.BusinessId == businessId)
            .Select(c => new CustomerDto(c.Id, c.Name, c.PhoneNumber, c.Notes, c.BusinessId))
            .ToListAsync();

        return Ok(customers);
    }
}



