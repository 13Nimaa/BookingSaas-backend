namespace Application.Dtos.WorkingHour;

/// <param name="Date">Gregorian ISO date (yyyy-MM-dd). Convert to Jalali on the frontend for display.</param>
public sealed record WorkingHourDto(
    int Id,
    int EmployeeId,
    DateOnly Date,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime
);
