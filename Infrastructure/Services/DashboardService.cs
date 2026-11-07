using Application.Dtos.Business;
using BookingSaas_backend.Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private const int TodayBookingsWidgetLimit = 5;
    private const int TopServicesLimit = 5;

    /// <summary>Iran business week starts Saturday.</summary>
    private const DayOfWeek BusinessWeekStart = DayOfWeek.Saturday;

    private static readonly BookingStatus[] OperationalStatuses =
    [
        BookingStatus.Pending,
        BookingStatus.Confirmed,
        BookingStatus.Completed
    ];

    private static readonly string[] WeekdayKeys =
    [
        "sunday",
        "monday",
        "tuesday",
        "wednesday",
        "thursday",
        "friday",
        "saturday"
    ];

    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db) => _db = db;

    public async Task<BusinessDashboardDto?> GetForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default)
    {
        var business = await _db.Businesses
            .AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .Select(b => new { b.Id, b.TimeZoneId })
            .FirstOrDefaultAsync(cancellationToken);

        if (business is null)
            return null;

        if (!TryResolveTimeZone(business.TimeZoneId, out var timeZone))
            throw new InvalidOperationException("منطقه زمانی کسب‌وکار معتبر نیست.");

        var utcNow = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            timeZone);
        var today = DateOnly.FromDateTime(localNow);

        if (!TryLocalToUtc(today, TimeOnly.MinValue, timeZone, out var todayStartUtc)
            || !TryLocalToUtc(today.AddDays(1), TimeOnly.MinValue, timeZone, out var todayEndUtc))
        {
            throw new InvalidOperationException("تبدیل تاریخ به منطقه زمانی کسب‌وکار ممکن نیست.");
        }

        var weekStart = GetWeekStart(today);
        var weekEnd = weekStart.AddDays(7);
        if (!TryLocalToUtc(weekStart, TimeOnly.MinValue, timeZone, out var weekStartUtc)
            || !TryLocalToUtc(weekEnd, TimeOnly.MinValue, timeZone, out var weekEndUtc))
        {
            throw new InvalidOperationException("تبدیل تاریخ به منطقه زمانی کسب‌وکار ممکن نیست.");
        }

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        if (!TryLocalToUtc(monthStart, TimeOnly.MinValue, timeZone, out var monthStartUtc)
            || !TryLocalToUtc(monthEnd, TimeOnly.MinValue, timeZone, out var monthEndUtc))
        {
            throw new InvalidOperationException("تبدیل تاریخ به منطقه زمانی کسب‌وکار ممکن نیست.");
        }

        var businessId = business.Id;

        var todayBookingsCount = await _db.Bookings
            .AsNoTracking()
            .CountAsync(
                b => b.BusinessId == businessId
                     && OperationalStatuses.Contains(b.Status)
                     && b.StartTime >= todayStartUtc
                     && b.StartTime < todayEndUtc,
                cancellationToken);

        var todayRevenueRows = await _db.Bookings
            .AsNoTracking()
            .Where(b =>
                b.BusinessId == businessId
                && b.Status == BookingStatus.Completed
                && b.StartTime >= todayStartUtc
                && b.StartTime < todayEndUtc
                && b.Price != null)
            .Select(b => b.Price!.Value)
            .ToListAsync(cancellationToken);

        decimal? todayEstimatedRevenue = todayRevenueRows.Count == 0
            ? null
            : todayRevenueRows.Sum();

        // Empty completed day with no priced rows: if there were completed bookings all with null
        // prices, revenue is unavailable (null). Distinguish via a cheap existence check.
        if (todayEstimatedRevenue is null)
        {
            var completedTodayExists = await _db.Bookings
                .AsNoTracking()
                .AnyAsync(
                    b => b.BusinessId == businessId
                         && b.Status == BookingStatus.Completed
                         && b.StartTime >= todayStartUtc
                         && b.StartTime < todayEndUtc,
                    cancellationToken);

            if (!completedTodayExists)
                todayEstimatedRevenue = 0m;
        }

        var customerCount = await _db.Customers
            .AsNoTracking()
            .CountAsync(c => c.BusinessId == businessId, cancellationToken);

        var capacity = await BuildCapacityAsync(
            businessId,
            today,
            todayStartUtc,
            todayEndUtc,
            timeZone,
            cancellationToken);

        var weeklyRevenue = await BuildWeeklyRevenueAsync(
            businessId,
            weekStart,
            weekStartUtc,
            weekEndUtc,
            timeZone,
            cancellationToken);

        var todayBookings = await BuildTodayBookingsAsync(
            businessId,
            todayStartUtc,
            todayEndUtc,
            todayBookingsCount,
            cancellationToken);

        var topServices = await BuildTopServicesAsync(
            businessId,
            monthStart,
            monthEnd.AddDays(-1),
            monthStartUtc,
            monthEndUtc,
            cancellationToken);

        return new BusinessDashboardDto(
            new DashboardSummaryDto(
                todayBookingsCount,
                todayEstimatedRevenue,
                customerCount),
            capacity,
            weeklyRevenue,
            todayBookings,
            topServices);
    }

    private async Task<DashboardCapacityDto> BuildCapacityAsync(
        int businessId,
        DateOnly today,
        DateTime todayStartUtc,
        DateTime todayEndUtc,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var eligibleEmployeeIds = await _db.EmployeeServices
            .AsNoTracking()
            .Where(es => es.Employee.BusinessId == businessId)
            .Select(es => es.EmployeesId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (eligibleEmployeeIds.Count == 0)
        {
            return new DashboardCapacityDto(0, 0, null);
        }

        var workingHours = await _db.WorkingHours
            .AsNoTracking()
            .Where(w =>
                eligibleEmployeeIds.Contains(w.EmployeeId)
                && w.Date == today)
            .Select(w => new { w.EmployeeId, w.StartTime, w.EndTime })
            .ToListAsync(cancellationToken);

        var availableMinutes = 0;
        var intervalsByEmployee = new Dictionary<int, List<(DateTime Start, DateTime End)>>();

        foreach (var hour in workingHours)
        {
            if (!TryLocalToUtc(today, hour.StartTime, timeZone, out var startUtc)
                || !TryLocalToUtc(today, hour.EndTime, timeZone, out var endUtc)
                || endUtc <= startUtc)
            {
                continue;
            }

            availableMinutes += (int)(endUtc - startUtc).TotalMinutes;

            if (!intervalsByEmployee.TryGetValue(hour.EmployeeId, out var list))
            {
                list = [];
                intervalsByEmployee[hour.EmployeeId] = list;
            }

            list.Add((startUtc, endUtc));
        }

        if (availableMinutes == 0)
        {
            return new DashboardCapacityDto(0, 0, null);
        }

        var bookings = await _db.Bookings
            .AsNoTracking()
            .Where(b =>
                b.BusinessId == businessId
                && eligibleEmployeeIds.Contains(b.EmployeeId)
                && OperationalStatuses.Contains(b.Status)
                && b.StartTime < todayEndUtc
                && b.EndTime > todayStartUtc)
            .Select(b => new { b.EmployeeId, b.StartTime, b.EndTime })
            .ToListAsync(cancellationToken);

        var occupiedMinutes = 0;
        foreach (var group in bookings.GroupBy(b => b.EmployeeId))
        {
            if (!intervalsByEmployee.TryGetValue(group.Key, out var workIntervals))
                continue;

            var clipped = new List<(DateTime Start, DateTime End)>();
            foreach (var booking in group)
            {
                foreach (var work in workIntervals)
                {
                    var start = booking.StartTime > work.Start ? booking.StartTime : work.Start;
                    var end = booking.EndTime < work.End ? booking.EndTime : work.End;
                    if (end > start)
                        clipped.Add((start, end));
                }
            }

            occupiedMinutes += SumMergedMinutes(clipped);
        }

        if (occupiedMinutes > availableMinutes)
            occupiedMinutes = availableMinutes;

        var percent = Math.Round(
            occupiedMinutes * 100m / availableMinutes,
            2,
            MidpointRounding.AwayFromZero);

        return new DashboardCapacityDto(occupiedMinutes, availableMinutes, percent);
    }

    private async Task<IReadOnlyList<WeeklyRevenueDayDto>> BuildWeeklyRevenueAsync(
        int businessId,
        DateOnly weekStart,
        DateTime weekStartUtc,
        DateTime weekEndUtc,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Bookings
            .AsNoTracking()
            .Where(b =>
                b.BusinessId == businessId
                && b.Status == BookingStatus.Completed
                && b.StartTime >= weekStartUtc
                && b.StartTime < weekEndUtc)
            .Select(b => new { b.StartTime, b.Price })
            .ToListAsync(cancellationToken);

        var byDate = new Dictionary<DateOnly, (decimal Revenue, int Count)>();
        for (var i = 0; i < 7; i++)
            byDate[weekStart.AddDays(i)] = (0m, 0);

        foreach (var row in rows)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(row.StartTime, DateTimeKind.Utc),
                timeZone);
            var localDate = DateOnly.FromDateTime(local);
            if (!byDate.TryGetValue(localDate, out var agg))
                continue;

            var count = agg.Count + 1;
            var revenue = agg.Revenue;
            if (row.Price is not null)
                revenue += row.Price.Value;

            byDate[localDate] = (revenue, count);
        }

        return Enumerable.Range(0, 7)
            .Select(i =>
            {
                var date = weekStart.AddDays(i);
                var (revenue, count) = byDate[date];
                return new WeeklyRevenueDayDto(
                    date,
                    date.DayOfWeek,
                    WeekdayKeys[(int)date.DayOfWeek],
                    revenue,
                    count);
            })
            .ToList();
    }

    private async Task<DashboardTodayBookingsDto> BuildTodayBookingsAsync(
        int businessId,
        DateTime todayStartUtc,
        DateTime todayEndUtc,
        int totalCount,
        CancellationToken cancellationToken)
    {
        var items = await _db.Bookings
            .AsNoTracking()
            .Where(b =>
                b.BusinessId == businessId
                && OperationalStatuses.Contains(b.Status)
                && b.StartTime >= todayStartUtc
                && b.StartTime < todayEndUtc)
            .OrderBy(b => b.StartTime)
            .ThenBy(b => b.Id)
            .Take(TodayBookingsWidgetLimit)
            .Select(b => new DashboardBookingItemDto(
                b.Id,
                b.Customer.Name,
                b.Service.Title,
                b.Employee.User.FullName,
                b.StartTime,
                b.EndTime,
                b.Status,
                b.Price))
            .ToListAsync(cancellationToken);

        return new DashboardTodayBookingsDto(totalCount, items);
    }

    private async Task<DashboardTopServicesDto> BuildTopServicesAsync(
        int businessId,
        DateOnly periodStart,
        DateOnly periodEndInclusive,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken)
    {
        var grouped = await _db.Bookings
            .AsNoTracking()
            .Where(b =>
                b.BusinessId == businessId
                && b.Status == BookingStatus.Completed
                && b.StartTime >= periodStartUtc
                && b.StartTime < periodEndUtc)
            .GroupBy(b => new { b.ServiceId, b.Service.Title })
            .Select(g => new
            {
                g.Key.ServiceId,
                g.Key.Title,
                BookingCount = g.Count()
            })
            .OrderByDescending(x => x.BookingCount)
            .ThenBy(x => x.Title)
            .Take(TopServicesLimit)
            .ToListAsync(cancellationToken);

        var totalInGroups = grouped.Sum(x => x.BookingCount);

        var items = grouped
            .Select(x => new DashboardTopServiceItemDto(
                x.ServiceId,
                x.Title,
                x.BookingCount,
                totalInGroups == 0
                    ? 0m
                    : Math.Round(
                        x.BookingCount * 100m / totalInGroups,
                        2,
                        MidpointRounding.AwayFromZero)))
            .ToList();

        return new DashboardTopServicesDto(periodStart, periodEndInclusive, items);
    }

    private static int SumMergedMinutes(List<(DateTime Start, DateTime End)> intervals)
    {
        if (intervals.Count == 0)
            return 0;

        intervals.Sort((a, b) => a.Start.CompareTo(b.Start));

        var total = 0;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;

        for (var i = 1; i < intervals.Count; i++)
        {
            var (start, end) = intervals[i];
            if (start < currentEnd)
            {
                if (end > currentEnd)
                    currentEnd = end;
            }
            else
            {
                total += (int)(currentEnd - currentStart).TotalMinutes;
                currentStart = start;
                currentEnd = end;
            }
        }

        total += (int)(currentEnd - currentStart).TotalMinutes;
        return total;
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)BusinessWeekStart + 7) % 7;
        return date.AddDays(-diff);
    }

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
            utc = default;
            return false;
        }
    }
}
