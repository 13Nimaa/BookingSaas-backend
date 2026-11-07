using Application.Dtos.Business;

namespace BookingSaas_backend.Application.Interfaces;

public interface IDashboardService
{
    Task<BusinessDashboardDto?> GetForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);
}
