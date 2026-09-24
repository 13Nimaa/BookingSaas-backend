using Domain.Entities;

namespace BookingSaas_backend.Application.Interfaces;

public interface ICustomerService
{
    Task<Customer> GetOrCreateForUserAsync(
        string userId,
        int businessId);

    Task<Customer> GetOrCreateForGuestAsync(
        int businessId,
        string name,
        string phoneNumber);
}