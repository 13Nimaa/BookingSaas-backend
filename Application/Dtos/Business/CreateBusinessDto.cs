namespace Application.Dtos.Business;

public sealed record CreateBusinessDto(
    string Name,
    string City,
    string Address,
    string? Description);