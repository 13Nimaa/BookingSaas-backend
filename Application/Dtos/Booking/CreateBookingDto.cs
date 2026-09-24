namespace Application.Dtos.Booking;

public sealed record CreateBookingDto(
    int BusinessId,
    int ServiceId,
    int EmployeeId,
    DateTime StartTime,
    DateTime EndTime
);