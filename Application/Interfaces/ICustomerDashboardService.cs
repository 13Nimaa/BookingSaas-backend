using Application.Dtos.Customer;

namespace BookingSaas_backend.Application.Interfaces;

public interface ICustomerDashboardService
{
    Task<CustomerDashboardDto> GetAsync(
        string userId,
        CancellationToken cancellationToken = default);
}