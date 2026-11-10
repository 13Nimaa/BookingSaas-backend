using Application.Dtos.Customer;

namespace BookingSaas_backend.Application.Interfaces;

public interface IBusinessFavoriteService
{
    Task<IReadOnlyList<BusinessFavoriteDto>> ListAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<BusinessFavoriteDto> AddAsync(
        string userId,
        int businessId,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        string userId,
        int businessId,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
