namespace Domain.Entities;

public class WorkingHour
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    /// <summary>Gregorian calendar date. Frontend sends/receives ISO yyyy-MM-dd (convert from/to Jalali there).</summary>
    public DateOnly Date { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
