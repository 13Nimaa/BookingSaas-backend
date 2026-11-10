using Application.Dtos.Customer;
using BookingSaas_backend.Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingSaas_backend.Application.Services;

public sealed class CustomerDashboardService : ICustomerDashboardService
{
    private const int UpcomingWidgetLimit = 5;
    private const int RecentWidgetLimit = 5;

    private static readonly BookingStatus[] UpcomingStatuses =
    [
        BookingStatus.Pending,
        BookingStatus.Confirmed
    ];

    private readonly AppDbContext _db;
    private readonly IBusinessFavoriteService _favorites;

    public CustomerDashboardService(AppDbContext db, IBusinessFavoriteService favorites)
    {
        _db = db;
        _favorites = favorites;
    }

    public async Task<CustomerDashboardDto> GetAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        var mine = _db.Bookings
            .AsNoTracking()
            .Where(b => b.Customer.UserId == userId);

        var totalBookingsCount = await mine.CountAsync(cancellationToken);

        var upcomingQuery = mine.Where(b =>
            UpcomingStatuses.Contains(b.Status) &&
            b.StartTime >= utcNow);

        var upcomingBookingsCount = await upcomingQuery.CountAsync(cancellationToken);

        var upcomingItems = await upcomingQuery
            .OrderBy(b => b.StartTime)
            .ThenBy(b => b.Id)
            .Take(UpcomingWidgetLimit)
            .Select(b => new CustomerDashboardBookingItemDto(
                b.Id,
                b.StartTime,
                b.EndTime,
                b.Status,
                b.BusinessId,
                b.Business.Name,
                b.Business.ProfileImageUrl,
                b.Service.Title,
                b.Employee.User.FullName))
            .ToListAsync(cancellationToken);

        var recentItems = await mine
            .OrderByDescending(b => b.StartTime)
            .ThenByDescending(b => b.Id)
            .Take(RecentWidgetLimit)
            .Select(b => new CustomerDashboardBookingItemDto(
                b.Id,
                b.StartTime,
                b.EndTime,
                b.Status,
                b.BusinessId,
                b.Business.Name,
                b.Business.ProfileImageUrl,
                b.Service.Title,
                b.Employee.User.FullName))
            .ToListAsync(cancellationToken);

        var favoriteSalonsCount = await _favorites.CountAsync(userId, cancellationToken);

        return new CustomerDashboardDto(
            new CustomerDashboardSummaryDto(
                upcomingBookingsCount,
                totalBookingsCount,
                favoriteSalonsCount),
            new CustomerDashboardUpcomingDto(upcomingBookingsCount, upcomingItems),
            recentItems);
    }
}
