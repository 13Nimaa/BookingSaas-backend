using Domain.Entities;

namespace Application.Dtos.Booking;

public class BookingMineQuery
{
    public BookingStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
