namespace Application.Dtos.Service;

public sealed record ServiceDto(
    int Id,
    string Title,
    decimal Price,
    int DurationMinutes,
    int BusinessId);