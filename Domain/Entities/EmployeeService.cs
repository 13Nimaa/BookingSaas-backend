namespace Domain.Entities;

public class EmployeeService
{
    public int EmployeesId { get; set; }
    public int ServicesId { get; set; }

    public decimal? PriceOverride { get; set; }
    public int? DurationMinutesOverride { get; set; }

    public Employee Employee { get; set; } = default!;
    public Service Service { get; set; } = default!;
}
