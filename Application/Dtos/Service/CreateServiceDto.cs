namespace Application.Dtos.Service;

public sealed record CreateServiceDto(
    string Title,
    decimal Price,
    int DurationMinutes);