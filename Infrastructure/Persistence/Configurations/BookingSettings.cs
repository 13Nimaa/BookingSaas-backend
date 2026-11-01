namespace BookingSaas_backend.Infrastructure.Configuration;

public sealed class BookingSettings
{
    public const string SectionName = "Booking";

    public int SlotIntervalMinutes { get; set; } = 15;

    public int PendingExpirationMinutes { get; set; } = 30;

    public int ExpirationPollingIntervalMinutes { get; set; } = 1;
}
