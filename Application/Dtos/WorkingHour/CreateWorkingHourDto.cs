namespace Application.Dtos.WorkingHour;

/// <param name="Date">Gregorian ISO date (yyyy-MM-dd). Convert Jalali on the frontend before sending.</param>
public sealed record CreateWorkingHourDto(
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime
);
