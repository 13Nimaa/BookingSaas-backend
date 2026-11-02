namespace Application.Dtos.Employee;

public sealed record AssignServiceDto(
    int ServiceId,
    decimal? PriceOverride = null,
    int? DurationMinutesOverride = null);
