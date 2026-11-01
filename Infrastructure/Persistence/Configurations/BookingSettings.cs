namespace BookingSaas_backend.Infrastructure.Configuration;

public sealed class BookingSettings
{
    public const string SectionName = "Booking";

    public int SlotIntervalMinutes { get; set; } = 15;
}
