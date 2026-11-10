using Domain.Entities;

namespace Application.Dtos.Customer;

public sealed record CustomerDashboardDto(
    CustomerDashboardSummaryDto Summary,
    CustomerDashboardUpcomingDto UpcomingBookings,
    IReadOnlyList<CustomerDashboardBookingItemDto> RecentBookings);

public sealed record CustomerDashboardSummaryDto(
    int UpcomingBookingsCount,
    int TotalBookingsCount,
    int FavoriteSalonsCount);

public sealed record CustomerDashboardUpcomingDto(
    int TotalCount,
    IReadOnlyList<CustomerDashboardBookingItemDto> Items);

public sealed record CustomerDashboardBookingItemDto(
    int Id,
    DateTime StartTime,
    DateTime EndTime,
    BookingStatus Status,
    int BusinessId,
    string BusinessName,
    string? BusinessProfileImageUrl,
    string ServiceName,
    string EmployeeName);
