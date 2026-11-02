namespace Application.Dtos.Employee;

public sealed record EmployeeServiceDto(
    int Id,
    string Title,
    decimal DefaultPrice,
    int DefaultDurationMinutes,
    decimal? PriceOverride,
    int? DurationMinutesOverride,
    decimal EffectivePrice,
    int EffectiveDurationMinutes,
    int BusinessId);
