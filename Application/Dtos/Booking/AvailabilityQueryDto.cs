namespace Application.Dtos.Booking;

public sealed record AvailabilityQueryDto(
    int BusinessId,
    int ServiceId,
    int EmployeeId,
    DateOnly Date);
