namespace Application.Dtos.WorkingHour;

public sealed record WorkingHourDto(
    int Id,
    int EmployeeId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime
);