using Application.Dtos.WorkingHour;

namespace Application.Dtos.Employee;

/// <summary>
/// Anonymous-safe employee shape for public business detail. No phone / userId.
/// </summary>
public sealed record PublicEmployeeDto(
    int Id,
    string FullName,
    string? AvatarUrl,
    IReadOnlyList<PublicEmployeeServiceDto> Services,
    IReadOnlyList<WorkingHourDto> WorkingHours);

/// <summary>
/// Assignment summary for eligible staff + booking pricing/duration.
/// </summary>
public sealed record PublicEmployeeServiceDto(
    int Id,
    string Title,
    decimal EffectivePrice,
    int EffectiveDurationMinutes);
