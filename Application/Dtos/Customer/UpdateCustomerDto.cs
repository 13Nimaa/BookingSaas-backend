namespace Application.Dtos.Customer;

public sealed record UpdateCustomerDto(
    string Name,
    string? PhoneNumber,
    string? Email,
    string? Notes,
    bool IsActive);
