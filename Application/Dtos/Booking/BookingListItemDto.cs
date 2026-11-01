using Domain.Entities;

namespace Application.Dtos.Booking;

public sealed record BookingListItemDto(
    int Id,
    DateTime StartTime,
    DateTime EndTime,
    BookingStatus Status,
    BookingCreatorType CreatorType,
    string CustomerName,
    string? CustomerPhone,
    string ServiceName,
    int ServiceDurationMinutes,
    string EmployeeName,
    int BusinessId);
