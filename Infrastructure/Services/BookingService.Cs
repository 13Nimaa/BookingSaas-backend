using Application.Common;
using Application.Dtos.Booking;
using BookingSaas_backend.Application.Interfaces;
using BookingSaas_backend.Infrastructure.Configuration;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BookingSaas_backend.Application.Services;

public class BookingService : IBookingService
{
    private const string OccupiedSlotMessage = "این بازه زمانی قبلاً رزرو شده است.";

    private static readonly BookingStatus[] OccupyingStatuses =
    [
        BookingStatus.Pending,
        BookingStatus.Confirmed
    ];

    private readonly AppDbContext _context;
    private readonly ICustomerService _customerService;
    private readonly int _slotIntervalMinutes;
    private readonly int _pendingExpirationMinutes;

    public BookingService(
        AppDbContext context,
        ICustomerService customerService,
        IOptions<BookingSettings> bookingSettings)
    {
        _context = context;
        _customerService = customerService;
        _slotIntervalMinutes = Math.Max(1, bookingSettings.Value.SlotIntervalMinutes);
        _pendingExpirationMinutes = Math.Max(1, bookingSettings.Value.PendingExpirationMinutes);
    }

    public async Task<Booking> CreateForUserAsync(
        string userId,
        CreateBookingDto dto)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
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
            await transaction.CommitAsync();

            return booking;
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException(OccupiedSlotMessage);
        }
    }

    public async Task<Booking> CreateForGuestAsync(
        CreateGuestBookingDto dto)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
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
            await transaction.CommitAsync();

            return booking;
        }
        catch (DbUpdateException ex) when (IsExclusionViolation(ex))
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException(OccupiedSlotMessage);
        }
    }

    public async Task<Booking> UpdateStatusAsync(
        int bookingId,
        string ownerId,
        UpdateBookingStatusDto dto)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking is null)
            throw new KeyNotFoundException("رزرو یافت نشد.");

        var ownsBusiness = await _context.Businesses
            .AnyAsync(b => b.Id == booking.BusinessId && b.OwnerId == ownerId);

        if (!ownsBusiness)
            throw new UnauthorizedAccessException();

        if (!IsAllowedTransition(booking.Status, dto.Status))
            throw new ArgumentException("تغییر وضعیت مجاز نیست.");

        booking.Status = dto.Status;
        await _context.SaveChangesAsync();

        return booking;
    }

    public async Task<PagedResult<BookingListItemDto>> ListAsync(
        string userId,
        bool isOwner,
        bool isEmployee,
        BookingListQuery query)
    {
        IQueryable<Booking> bookings = _context.Bookings.AsNoTracking();

        if (isOwner)
        {
            var businessId = await _context.Businesses
                .AsNoTracking()
                .Where(b => b.OwnerId == userId)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync();

            if (businessId is null)
                return EmptyPage(query);

            bookings = bookings.Where(b => b.BusinessId == businessId.Value);

            if (query.EmployeeId is not null)
                bookings = bookings.Where(b => b.EmployeeId == query.EmployeeId);
        }
        else if (isEmployee)
        {
            var employeeId = await _context.Employees
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync();

            if (employeeId is null)
                return EmptyPage(query);

            // Always scope to the caller's employee row; ignore mismatched employeeId.
            bookings = bookings.Where(b => b.EmployeeId == employeeId.Value);
        }
        else
        {
            throw new UnauthorizedAccessException();
        }

        if (query.Status is not null)
            bookings = bookings.Where(b => b.Status == query.Status);

        if (query.CustomerId is not null)
            bookings = bookings.Where(b => b.CustomerId == query.CustomerId);

        if (query.ServiceId is not null)
            bookings = bookings.Where(b => b.ServiceId == query.ServiceId);

        if (query.FromDate is not null)
            bookings = bookings.Where(b => b.StartTime >= query.FromDate);

        if (query.ToDate is not null)
            bookings = bookings.Where(b => b.StartTime <= query.ToDate);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{search}%";
            bookings = bookings.Where(b =>
                EF.Functions.ILike(b.Customer.Name, pattern) ||
                (b.Customer.PhoneNumber != null &&
                 EF.Functions.ILike(b.Customer.PhoneNumber, pattern)));
        }

        var totalCount = await bookings.CountAsync();
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var items = await bookings
            .OrderByDescending(b => b.StartTime)
            .ThenByDescending(b => b.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => new BookingListItemDto(
                b.Id,
                b.StartTime,
                b.EndTime,
                b.Status,
                b.CreatorType,
                b.Customer.Name,
                b.Customer.PhoneNumber,
                b.Service.Title,
                b.Service.DurationMinutes,
                b.Employee.User.FullName,
                b.BusinessId))
            .ToListAsync();

        return new PagedResult<BookingListItemDto>(
            items, query.Page, query.PageSize, totalCount, totalPages);
    }

    public async Task<PagedResult<CustomerBookingListItemDto>> ListMineAsync(
        string userId,
        BookingMineQuery query)
    {
        // Ownership is resolved only via Customer.UserId from JWT — never client-supplied ids.
        // A user may have Customer rows across multiple businesses; guest rows (UserId null) are excluded.
        IQueryable<Booking> bookings = _context.Bookings
            .AsNoTracking()
            .Where(b => b.Customer.UserId == userId);

        if (query.Status is not null)
            bookings = bookings.Where(b => b.Status == query.Status);

        if (query.FromDate is not null)
            bookings = bookings.Where(b => b.StartTime >= query.FromDate);

        if (query.ToDate is not null)
            bookings = bookings.Where(b => b.StartTime <= query.ToDate);

        var totalCount = await bookings.CountAsync();
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var items = await bookings
            .OrderByDescending(b => b.StartTime)
            .ThenByDescending(b => b.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => new CustomerBookingListItemDto(
                b.Id,
                b.StartTime,
                b.EndTime,
                b.Status,
                b.CreatorType,
                b.Business.Name,
                b.BusinessId,
                b.Service.Title,
                b.Service.DurationMinutes,
                b.Employee.User.FullName))
            .ToListAsync();

        return new PagedResult<CustomerBookingListItemDto>(
            items, query.Page, query.PageSize, totalCount, totalPages);
    }

    public async Task<BookingDetailsDto> GetByIdAsync(
        int bookingId,
        string userId,
        bool isOwner,
        bool isEmployee)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => new BookingDetailsDto(
                b.Id,
                b.BusinessId,
                b.StartTime,
                b.EndTime,
                b.Status,
                b.CreatorType,
                b.CreatedAt,
                b.Notes,
                new BookingDetailsCustomerDto(
                    b.Customer.Id,
                    b.Customer.Name,
                    b.Customer.PhoneNumber),
                new BookingDetailsServiceDto(
                    b.Service.Id,
                    b.Service.Title,
                    b.Service.DurationMinutes,
                    b.Service.Price),
                new BookingDetailsEmployeeDto(
                    b.Employee.Id,
                    b.Employee.User.FullName,
                    b.Employee.User.ProfileImageUrl),
                new BookingDetailsBusinessDto(
                    b.Business.Id,
                    b.Business.Name)))
            .FirstOrDefaultAsync();

        if (booking is null)
            throw new KeyNotFoundException("رزرو یافت نشد.");

        if (isOwner)
        {
            var ownsBusiness = await _context.Businesses
                .AsNoTracking()
                .AnyAsync(b => b.Id == booking.BusinessId && b.OwnerId == userId);

            if (!ownsBusiness)
                throw new UnauthorizedAccessException();

            return booking;
        }

        if (isEmployee)
        {
            var employeeId = await _context.Employees
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync();

            if (employeeId is null || booking.Employee.Id != employeeId.Value)
                throw new UnauthorizedAccessException();

            return booking;
        }

        throw new UnauthorizedAccessException();
    }

    public Task<int> ExpirePendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-_pendingExpirationMinutes);

        return _context.Bookings
            .Where(b => b.Status == BookingStatus.Pending && b.CreatedAt <= cutoff)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.Status, BookingStatus.Expired),
                cancellationToken);
    }

    private static PagedResult<BookingListItemDto> EmptyPage(BookingListQuery query) =>
        new([], query.Page, query.PageSize, 0, 0);

    public async Task<IReadOnlyList<AvailabilitySlotDto>> GetAvailabilityAsync(
        AvailabilityQueryDto query)
    {
        var (business, _, employee, effectiveDurationMinutes) = await ValidateAvailabilitySelectionAsync(
            query.BusinessId,
            query.ServiceId,
            query.EmployeeId);

        if (!TryResolveTimeZone(business.TimeZoneId, out var timeZone))
            throw new InvalidOperationException("منطقه زمانی کسب‌وکار معتبر نیست.");

        var workingHours = await _context.WorkingHours
            .AsNoTracking()
            .Where(w => w.EmployeeId == employee.Id && w.Date == query.Date)
            .OrderBy(w => w.StartTime)
            .ToListAsync();

        if (workingHours.Count == 0)
            throw new InvalidOperationException("ساعت کاری برای این تاریخ تعریف نشده است.");

        if (!TryLocalToUtc(query.Date, TimeOnly.MinValue, timeZone, out var dayStartUtc)
            || !TryLocalToUtc(query.Date.AddDays(1), TimeOnly.MinValue, timeZone, out var dayEndUtc))
        {
            throw new InvalidOperationException("تبدیل تاریخ به منطقه زمانی کسب‌وکار ممکن نیست.");
        }

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Where(b =>
                b.EmployeeId == employee.Id &&
                OccupyingStatuses.Contains(b.Status) &&
                b.StartTime < dayEndUtc &&
                b.EndTime > dayStartUtc)
            .Select(b => new { b.StartTime, b.EndTime })
            .ToListAsync();

        var utcNow = DateTime.UtcNow;
        var duration = TimeSpan.FromMinutes(effectiveDurationMinutes);
        var step = TimeSpan.FromMinutes(_slotIntervalMinutes);
        var slots = new List<AvailabilitySlotDto>();

        foreach (var interval in workingHours)
        {
            if (!TryLocalToUtc(query.Date, interval.StartTime, timeZone, out var intervalStartUtc)
                || !TryLocalToUtc(query.Date, interval.EndTime, timeZone, out var intervalEndUtc))
            {
                continue;
            }

            for (var slotStartUtc = intervalStartUtc;
                 slotStartUtc + duration <= intervalEndUtc;
                 slotStartUtc += step)
            {
                var slotEndUtc = slotStartUtc + duration;

                if (slotStartUtc < utcNow)
                    continue;

                var overlaps = bookings.Any(b =>
                    slotStartUtc < b.EndTime && b.StartTime < slotEndUtc);

                if (overlaps)
                    continue;

                slots.Add(new AvailabilitySlotDto(
                    ToBusinessOffset(slotStartUtc, timeZone),
                    ToBusinessOffset(slotEndUtc, timeZone)));
            }
        }

        return slots;
    }

    private async Task<(Business Business, Service Service, Employee Employee, int EffectiveDurationMinutes)>
        ValidateAvailabilitySelectionAsync(
            int businessId,
            int serviceId,
            int employeeId)
    {
        var business = await _context.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == businessId);

        if (business is null)
            throw new KeyNotFoundException("کسب‌وکار یافت نشد.");

        var service = await _context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == serviceId);

        if (service is null)
            throw new KeyNotFoundException("خدمت یافت نشد.");

        if (service.BusinessId != businessId)
            throw new InvalidOperationException("این خدمت متعلق به کسب‌وکار دیگری است.");

        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee is null)
            throw new KeyNotFoundException("کارمند یافت نشد.");

        if (employee.BusinessId != businessId)
            throw new InvalidOperationException("این کارمند متعلق به کسب‌وکار دیگری است.");

        var assignment = await _context.EmployeeServices
            .AsNoTracking()
            .FirstOrDefaultAsync(es =>
                es.EmployeesId == employeeId &&
                es.ServicesId == serviceId);

        if (assignment is null)
            throw new InvalidOperationException("این کارمند به خدمت انتخاب‌شده تخصیص داده نشده است.");

        var effectiveDurationMinutes =
            assignment.DurationMinutesOverride ?? service.DurationMinutes;

        return (business, service, employee, effectiveDurationMinutes);
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

    private static bool TryResolveTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = null!;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = null!;
            return false;
        }
    }

    private static bool TryLocalToUtc(
        DateOnly date,
        TimeOnly time,
        TimeZoneInfo timeZone,
        out DateTime utc)
    {
        try
        {
            var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
            utc = TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
            return true;
        }
        catch (ArgumentException)
        {
            // Invalid local time during DST spring-forward gap.
            utc = default;
            return false;
        }
    }

    private static DateTimeOffset ToBusinessOffset(DateTime utc, TimeZoneInfo timeZone)
    {
        var utcDto = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        return TimeZoneInfo.ConvertTime(utcDto, timeZone);
    }

    private static bool IsAllowedTransition(BookingStatus from, BookingStatus to) =>
        (from, to) switch
        {
            (BookingStatus.Pending, BookingStatus.Confirmed) => true,
            (BookingStatus.Pending, BookingStatus.Cancelled) => true,
            (BookingStatus.Confirmed, BookingStatus.Completed) => true,
            (BookingStatus.Confirmed, BookingStatus.Cancelled) => true,
            _ => false
        };

    private static bool IsExclusionViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation };
}
