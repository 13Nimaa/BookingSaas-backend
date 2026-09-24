using BookingSaas_backend.Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;

    public CustomerService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Customer> GetOrCreateForUserAsync(
        string userId,
        int businessId)
    {
        // 1. Check if customer already exists
        // for this user and business
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c =>
                c.UserId == userId &&
                c.BusinessId == businessId);

        if (customer != null)
            return customer;

        // 2. Get ApplicationUser
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new KeyNotFoundException("User not found.");

        // 3. Check if a guest customer with the same
        // phone number exists for this business
        customer = await _context.Customers
            .FirstOrDefaultAsync(c =>
                c.BusinessId == businessId &&
                c.PhoneNumber == user.PhoneNumber &&
                c.UserId == null);

        if (customer != null)
        {
            // Connect existing guest customer
            // to the registered user
            customer.UserId = userId;

            await _context.SaveChangesAsync();

            return customer;
        }

        // 4. Create a new customer
        customer = new Customer
        {
            BusinessId = businessId,
            UserId = userId,
            Name = user.UserName ?? string.Empty,
            PhoneNumber = user.PhoneNumber
        };

        _context.Customers.Add(customer);

        await _context.SaveChangesAsync();

        return customer;
    }

    public async Task<Customer> GetOrCreateForGuestAsync(
        int businessId,
        string name,
        string phoneNumber)
    {
        // 1. Check if customer already exists
        // with the same phone number
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c =>
                c.BusinessId == businessId &&
                c.PhoneNumber == phoneNumber);

        if (customer != null)
            return customer;

        // 2. Create a new guest customer
        customer = new Customer
        {
            BusinessId = businessId,
            Name = name,
            PhoneNumber = phoneNumber,
            UserId = null
        };

        _context.Customers.Add(customer);

        await _context.SaveChangesAsync();

        return customer;
    }
}