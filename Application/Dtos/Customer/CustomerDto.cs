namespace Application.Dtos.Customer;

public sealed record CustomerDto(
    int Id,
    string Name,
    string? PhoneNumber,
    string? Notes,
    int BusinessId);