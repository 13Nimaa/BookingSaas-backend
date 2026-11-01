using Domain.Entities;

namespace Application.Dtos.Booking;

public class BookingListQuery
{
    public BookingStatus? Status { get; set; }
    public int? EmployeeId { get; set; }
    public int? CustomerId { get; set; }
    public int? ServiceId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
