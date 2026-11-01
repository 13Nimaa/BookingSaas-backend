using Domain.Entities;

namespace Application.Dtos.Booking;

public sealed record CustomerBookingListItemDto(
    int Id,
    DateTime StartTime,
    DateTime EndTime,
    BookingStatus Status,
    BookingCreatorType CreatorType,
    string BusinessName,
    int BusinessId,
    string ServiceName,
    int ServiceDurationMinutes,
    string EmployeeName);
