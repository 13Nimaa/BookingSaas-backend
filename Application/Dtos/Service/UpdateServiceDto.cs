namespace Application.Dtos.Service;

public sealed record UpdateServiceDto(
    string Title,
    decimal Price,
    int DurationMinutes);
