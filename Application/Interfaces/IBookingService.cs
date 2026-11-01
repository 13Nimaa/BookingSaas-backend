using Application.Common;
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

    Task<PagedResult<BookingListItemDto>> ListAsync(
        string userId,
        bool isOwner,
        bool isEmployee,
        BookingListQuery query);

    Task<BookingDetailsDto> GetByIdAsync(
        int bookingId,
        string userId,
        bool isOwner,
        bool isEmployee);

    Task<int> ExpirePendingBookingsAsync(CancellationToken cancellationToken = default);
}
