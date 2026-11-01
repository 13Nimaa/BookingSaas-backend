namespace Application.Dtos.Booking;

/// <summary>
/// Bookable slot boundaries as ISO 8601 DateTimeOffset (business-local offset).
/// </summary>
public sealed record AvailabilitySlotDto(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);
