namespace Application.Dtos.Customer;

public sealed record CreateCustomerDto(
    string Name,
    string? PhoneNumber,
    string? Email,
    string? Notes);
