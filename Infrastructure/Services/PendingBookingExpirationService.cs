using BookingSaas_backend.Application.Interfaces;
using BookingSaas_backend.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookingSaas_backend.Infrastructure.Services;

public sealed class PendingBookingExpirationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingBookingExpirationService> _logger;
    private readonly TimeSpan _pollingInterval;

    public PendingBookingExpirationService(
        IServiceScopeFactory scopeFactory,
        IOptions<BookingSettings> settings,
        ILogger<PendingBookingExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollingInterval = TimeSpan.FromMinutes(
            Math.Max(1, settings.Value.ExpirationPollingIntervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                var expiredCount = await bookingService.ExpirePendingBookingsAsync(stoppingToken);

                if (expiredCount > 0)
                    _logger.LogInformation("Expired {Count} pending booking(s).", expiredCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending booking expiration failed.");
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
