namespace Application.Dtos.Customer;

public sealed record BusinessFavoriteDto(
    int BusinessId,
    string Name,
    string City,
    string? ProfileImageUrl,
    DateTime FavoritedAt);

public sealed record AddBusinessFavoriteDto(int BusinessId);
