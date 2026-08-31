namespace Application.Dtos.Business;

public sealed record UpdateBusinessDto(
    string Name,
    string City,
    string Address,
    string? Description);