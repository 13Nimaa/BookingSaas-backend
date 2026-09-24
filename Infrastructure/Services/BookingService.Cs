using Application.Dtos.Booking;
using BookingSaas_backend.Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;

namespace BookingSaas_backend.Application.Services;

public class BookingService : IBookingService
{
    private readonly AppDbContext _context;
    private readonly ICustomerService _customerService;

    public BookingService(
        AppDbContext context,
        ICustomerService customerService)
    {
        _context = context;
        _customerService = customerService;
    }

    public async Task<Booking> CreateForUserAsync(
        string userId,
        CreateBookingDto dto)
    {
        var customer = await _customerService.GetOrCreateForUserAsync(
            userId,
            dto.BusinessId);

        var booking = new Booking
        {
            BusinessId = dto.BusinessId,
            CustomerId = customer.Id,
            ServiceId = dto.ServiceId,
            EmployeeId = dto.EmployeeId,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Status = BookingStatus.Pending
        };

        _context.Bookings.Add(booking);

        await _context.SaveChangesAsync();

        return booking;
    }

    public async Task<Booking> CreateForGuestAsync(
        CreateGuestBookingDto dto)
    {
        var customer = await _customerService.GetOrCreateForGuestAsync(
            dto.BusinessId,
            dto.Name,
            dto.PhoneNumber);

        var booking = new Booking
        {
            BusinessId = dto.BusinessId,
            CustomerId = customer.Id,
            ServiceId = dto.ServiceId,
            EmployeeId = dto.EmployeeId,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Status = BookingStatus.Pending
        };

        _context.Bookings.Add(booking);

        await _context.SaveChangesAsync();

        return booking;
    }
}