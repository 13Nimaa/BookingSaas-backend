namespace Application.Dtos.Customer;

public sealed record CustomerDto(
    int Id,
    string Name,
    string? PhoneNumber,
    string? Email,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int BusinessId);
