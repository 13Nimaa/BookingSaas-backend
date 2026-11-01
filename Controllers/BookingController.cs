using System.Security.Claims;
using Application.Common;
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

    [Authorize(Roles = "BusinessOwner,Employee")]
    [HttpGet]
    public async Task<ActionResult<PagedResult<BookingListItemDto>>> List(
        [FromQuery] BookingListQuery query)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        try
        {
            var result = await _bookingService.ListAsync(
                userId,
                User.IsInRole("BusinessOwner"),
                User.IsInRole("Employee"),
                query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [Authorize(Roles = "BusinessOwner,Employee")]
    [HttpGet("{bookingId:int}")]
    public async Task<ActionResult<BookingDetailsDto>> GetById(int bookingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        try
        {
            var result = await _bookingService.GetByIdAsync(
                bookingId,
                userId,
                User.IsInRole("BusinessOwner"),
                User.IsInRole("Employee"));
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
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

    /// <summary>
    /// Returns bookable slots for the given business/service/employee/date.
    /// Slot timestamps are ISO 8601 DateTimeOffset values in the business timezone offset.
    /// Availability is informational and does not reserve a slot.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("availability")]
    public async Task<ActionResult<IReadOnlyList<AvailabilitySlotDto>>> GetAvailability(
        [FromQuery] AvailabilityQueryDto query)
    {
        try
        {
            var slots = await _bookingService.GetAvailabilityAsync(query);
            return Ok(slots);
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

    [Authorize(Roles = "BusinessOwner")]
    [HttpPatch("{bookingId:int}/status")]
    public async Task<ActionResult<BookingDto>> UpdateStatus(
        int bookingId,
        UpdateBookingStatusDto dto)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
            return Unauthorized();

        try
        {
            var booking = await _bookingService.UpdateStatusAsync(bookingId, ownerId, dto);
            return Ok(ToDto(booking));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
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
