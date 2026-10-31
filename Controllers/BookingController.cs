using System.Security.Claims;
using Application.Dtos.Booking;
using BookingSaas_backend.Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingSaas_backend.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        try
        {
            var booking = await _bookingService.CreateForUserAsync(userId, dto);
            return CreatedAtAction(nameof(Create), new { id = booking.Id }, ToDto(booking));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpPost("guest")]
    public async Task<ActionResult<BookingDto>> CreateGuest(CreateGuestBookingDto dto)
    {
        try
        {
            var booking = await _bookingService.CreateForGuestAsync(dto);
            return CreatedAtAction(nameof(CreateGuest), new { id = booking.Id }, ToDto(booking));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private static BookingDto ToDto(Booking booking) =>
        new(
            booking.Id,
            booking.BusinessId,
            booking.CustomerId,
            booking.ServiceId,
            booking.EmployeeId,
            booking.StartTime,
            booking.EndTime,
            booking.Notes,
            booking.Status,
            booking.CreatorType,
            booking.CreatedByUserId,
            booking.CreatedAt);
}
