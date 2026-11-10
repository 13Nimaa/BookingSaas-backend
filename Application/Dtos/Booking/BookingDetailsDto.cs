using Domain.Entities;

namespace Application.Dtos.Booking;

public sealed record BookingDetailsDto(
    int Id,
    int BusinessId,
    DateTime StartTime,
    DateTime EndTime,
    BookingStatus Status,
    BookingCreatorType CreatorType,
    DateTime CreatedAt,
    string? Notes,
    BookingDetailsCustomerDto Customer,
    BookingDetailsServiceDto Service,
    BookingDetailsEmployeeDto Employee,
    BookingDetailsBusinessDto Business);

public sealed record BookingDetailsCustomerDto(
    int Id,
    string Name,
    string? PhoneNumber);

public sealed record BookingDetailsServiceDto(
    int Id,
    string Name,
    int DurationMinutes,
    decimal? Price);

public sealed record BookingDetailsEmployeeDto(
    int Id,
    string Name,
    string? ProfileImageUrl);

public sealed record BookingDetailsBusinessDto(
    int Id,
    string Name,
    string? ProfileImageUrl);
