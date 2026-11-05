using Application.Dtos.Employee;

namespace Application.Dtos.Business;

/// <summary>
/// Public business detail including employees, assignments, and working hours.
/// </summary>
public sealed record BusinessDetailsDto(
    int Id,
    string Name,
    string City,
    string Address,
    string? Description,
    string? ProfileImageUrl,
    string OwnerId,
    IReadOnlyList<PublicEmployeeDto> Employees);
