namespace Application.Dtos.Booking;

public sealed record CreateGuestBookingDto(
    int BusinessId,
    int ServiceId,
    int EmployeeId,
    DateTime StartTime,
    DateTime EndTime,
    string Name,
    string PhoneNumber
);