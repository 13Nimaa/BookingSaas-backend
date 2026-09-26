namespace Application.Dtos.WorkingHour;

public sealed record CreateWorkingHourDto(
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime
);