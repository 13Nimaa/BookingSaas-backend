using Application.Dtos.Booking;
using BookingSaas_backend.Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
        var service = await ValidateSelectionAsync(
            dto.BusinessId,
            dto.ServiceId,
            dto.EmployeeId,
            dto.StartTime);

        var customer = await _customerService.GetOrCreateForUserAsync(
            userId,
            dto.BusinessId);

        var booking = BuildBooking(
            dto.BusinessId,
            customer.Id,
            dto.ServiceId,
            dto.EmployeeId,
            dto.StartTime,
            service.DurationMinutes,
            BookingCreatorType.Customer,
            userId);

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        return booking;
    }

    public async Task<Booking> CreateForGuestAsync(
        CreateGuestBookingDto dto)
    {
        var service = await ValidateSelectionAsync(
            dto.BusinessId,
            dto.ServiceId,
            dto.EmployeeId,
            dto.StartTime);

        var customer = await _customerService.GetOrCreateForGuestAsync(
            dto.BusinessId,
            dto.Name,
            dto.PhoneNumber);

        var booking = BuildBooking(
            dto.BusinessId,
            customer.Id,
            dto.ServiceId,
            dto.EmployeeId,
            dto.StartTime,
            service.DurationMinutes,
            BookingCreatorType.Guest,
            createdByUserId: null);

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        return booking;
    }

    private async Task<Service> ValidateSelectionAsync(
        int businessId,
        int serviceId,
        int employeeId,
        DateTime startTime)
    {
        if (startTime == default)
            throw new InvalidOperationException("زمان شروع معتبر نیست.");

        var businessExists = await _context.Businesses
            .AnyAsync(b => b.Id == businessId);

        if (!businessExists)
            throw new KeyNotFoundException("کسب‌وکار یافت نشد.");

        var service = await _context.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId);

        if (service is null)
            throw new KeyNotFoundException("خدمت یافت نشد.");

        if (service.BusinessId != businessId)
            throw new InvalidOperationException("این خدمت متعلق به کسب‌وکار دیگری است.");

        var employee = await _context.Employees
            .Include(e => e.Services)
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee is null)
            throw new KeyNotFoundException("کارمند یافت نشد.");

        if (employee.BusinessId != businessId)
            throw new InvalidOperationException("این کارمند متعلق به کسب‌وکار دیگری است.");

        if (employee.Services.All(s => s.Id != serviceId))
            throw new InvalidOperationException("این کارمند به خدمت انتخاب‌شده تخصیص داده نشده است.");

        return service;
    }

    private static Booking BuildBooking(
        int businessId,
        int customerId,
        int serviceId,
        int employeeId,
        DateTime startTime,
        int durationMinutes,
        BookingCreatorType creatorType,
        string? createdByUserId) =>
        new()
        {
            BusinessId = businessId,
            CustomerId = customerId,
            ServiceId = serviceId,
            EmployeeId = employeeId,
            StartTime = startTime,
            EndTime = startTime.AddMinutes(durationMinutes),
            Status = BookingStatus.Pending,
            CreatorType = creatorType,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
}
