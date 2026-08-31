namespace Application.Dtos.Business;

public sealed record BusinessDto(
    int Id,
    string Name,
    string City,
    string Address,
    string? Description,
    string OwnerId);