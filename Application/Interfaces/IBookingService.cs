using Application.Dtos.Booking;
using Domain.Entities;

namespace BookingSaas_backend.Application.Interfaces;

public interface IBookingService
{
    Task<Booking> CreateForUserAsync(
        string userId,
        CreateBookingDto dto);

    Task<Booking> CreateForGuestAsync(
        CreateGuestBookingDto dto);

    Task<IReadOnlyList<AvailabilitySlotDto>> GetAvailabilityAsync(
        AvailabilityQueryDto query);

    Task<Booking> UpdateStatusAsync(
        int bookingId,
        string ownerId,
        UpdateBookingStatusDto dto);

    Task<int> ExpirePendingBookingsAsync(CancellationToken cancellationToken = default);
}
