using Domain.Entities;

namespace Application.Dtos.Booking;

public sealed record BookingDto(
    int Id,
    int BusinessId,
    int CustomerId,
    int ServiceId,
    int EmployeeId,
    DateTime StartTime,
    DateTime EndTime,
    string? Notes,
    BookingStatus Status,
    BookingCreatorType CreatorType,
    string? CreatedByUserId,
    DateTime CreatedAt);
