namespace Application.Dtos.Profile;

public sealed record UserProfileDto(
    string Id,
    string FullName,
    string? PhoneNumber,
    string? Email,
    IList<string> Roles,
    int? BusinessId,
    int? EmployeeId
);

public sealed record UpdateProfileDto(
    string FullName,
    string? PhoneNumber,
    string? Email
);
