using Domain.Entities;

namespace Application.Dtos.Business;

public sealed record BusinessDashboardDto(
    DashboardSummaryDto Summary,
    DashboardCapacityDto? Capacity,
    IReadOnlyList<WeeklyRevenueDayDto> WeeklyRevenue,
    DashboardTodayBookingsDto TodayBookings,
    DashboardTopServicesDto TopServices);

public sealed record DashboardSummaryDto(
    int TodayBookingsCount,
    decimal? TodayEstimatedRevenue,
    int CustomerCount);

/// <summary>
/// Utilization of employee working minutes for the business-local current day.
/// Null UtilizationPercent when AvailableMinutes is zero (no scheduled capacity).
/// </summary>
public sealed record DashboardCapacityDto(
    int OccupiedMinutes,
    int AvailableMinutes,
    decimal? UtilizationPercent);

public sealed record WeeklyRevenueDayDto(
    DateOnly Date,
    DayOfWeek DayOfWeek,
    string WeekdayKey,
    decimal EstimatedRevenue,
    int CompletedBookingCount);

public sealed record DashboardTodayBookingsDto(
    int TotalCount,
    IReadOnlyList<DashboardBookingItemDto> Items);

public sealed record DashboardBookingItemDto(
    int Id,
    string? CustomerName,
    string ServiceTitle,
    string EmployeeName,
    DateTime StartTime,
    DateTime EndTime,
    BookingStatus Status,
    decimal? Price);

/// <summary>
/// Top services by completed booking count for the explicit PeriodStart–PeriodEnd
/// window (business-local calendar month).
/// </summary>
public sealed record DashboardTopServicesDto(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<DashboardTopServiceItemDto> Items);

public sealed record DashboardTopServiceItemDto(
    int ServiceId,
    string Title,
    int BookingCount,
    decimal Percentage);
