using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookingSaas_backend.Application.Interfaces;
using BookingSaas_backend.Infrastructure.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using BookingSaas_backend.Infrastructure.Configuration;
using Xunit;

namespace BookingSaas_backend.Tests;

[Collection(AuthCollection.Name)]
public sealed class BookingPendingExpirationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public BookingPendingExpirationTests(AuthFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _client = _fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Pending_older_than_threshold_becomes_Expired()
    {
        var setup = await CreateBookableSetupAsync("09128000001");
        var bookingId = await SeedBookingAsync(
            setup,
            BookingStatus.Pending,
            DateTime.UtcNow.AddMinutes(-31));

        var expired = await ExpireAsync();

        Assert.True(expired >= 1);
        Assert.Equal(BookingStatus.Expired, await GetStatusAsync(bookingId));
    }

    [Fact]
    public async Task Pending_exactly_at_cutoff_becomes_Expired()
    {
        var setup = await CreateBookableSetupAsync("09128000002");
        var bookingId = await SeedBookingAsync(
            setup,
            BookingStatus.Pending,
            DateTime.UtcNow.AddMinutes(-30));

        await ExpireAsync();

        Assert.Equal(BookingStatus.Expired, await GetStatusAsync(bookingId));
    }

    [Fact]
    public async Task Pending_younger_than_threshold_remains_Pending()
    {
        var setup = await CreateBookableSetupAsync("09128000003");
        var bookingId = await SeedBookingAsync(
            setup,
            BookingStatus.Pending,
            DateTime.UtcNow.AddMinutes(-10));

        var expired = await ExpireAsync();

        Assert.Equal(0, expired);
        Assert.Equal(BookingStatus.Pending, await GetStatusAsync(bookingId));
    }

    [Fact]
    public async Task Confirmed_older_than_threshold_remains_Confirmed()
    {
        var setup = await CreateBookableSetupAsync("09128000004");
        var bookingId = await SeedBookingAsync(
            setup,
            BookingStatus.Confirmed,
            DateTime.UtcNow.AddMinutes(-60));

        await ExpireAsync();

        Assert.Equal(BookingStatus.Confirmed, await GetStatusAsync(bookingId));
    }

    [Fact]
    public async Task Cancelled_remains_unchanged()
    {
        var setup = await CreateBookableSetupAsync("09128000005");
        var bookingId = await SeedBookingAsync(
            setup,
            BookingStatus.Cancelled,
            DateTime.UtcNow.AddMinutes(-60));

        await ExpireAsync();

        Assert.Equal(BookingStatus.Cancelled, await GetStatusAsync(bookingId));
    }

    [Fact]
    public async Task Completed_remains_unchanged()
    {
        var setup = await CreateBookableSetupAsync("09128000006");
        var bookingId = await SeedBookingAsync(
            setup,
            BookingStatus.Completed,
            DateTime.UtcNow.AddMinutes(-60));

        await ExpireAsync();

        Assert.Equal(BookingStatus.Completed, await GetStatusAsync(bookingId));
    }

    [Fact]
    public async Task Already_Expired_remains_unchanged()
    {
        var setup = await CreateBookableSetupAsync("09128000007");
        var bookingId = await SeedBookingAsync(
            setup,
            BookingStatus.Expired,
            DateTime.UtcNow.AddMinutes(-60));

        await ExpireAsync();

        Assert.Equal(BookingStatus.Expired, await GetStatusAsync(bookingId));
    }

    [Fact]
    public async Task Multiple_executions_are_idempotent()
    {
        var setup = await CreateBookableSetupAsync("09128000008");
        var bookingId = await SeedBookingAsync(
            setup,
            BookingStatus.Pending,
            DateTime.UtcNow.AddMinutes(-45));

        var first = await ExpireAsync();
        var second = await ExpireAsync();

        Assert.True(first >= 1);
        Assert.Equal(0, second);
        Assert.Equal(BookingStatus.Expired, await GetStatusAsync(bookingId));
    }

    [Fact]
    public async Task Concurrent_expire_calls_only_transition_Pending_rows()
    {
        var setup = await CreateBookableSetupAsync("09128000009");
        var pendingIds = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            pendingIds.Add(await SeedBookingAsync(
                setup,
                BookingStatus.Pending,
                DateTime.UtcNow.AddMinutes(-40 - i),
                startOffsetMinutes: i * 60));
        }

        var confirmedId = await SeedBookingAsync(
            setup,
            BookingStatus.Confirmed,
            DateTime.UtcNow.AddMinutes(-90),
            startOffsetMinutes: 400);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => ExpireAsync())
            .ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Sum());
        foreach (var id in pendingIds)
            Assert.Equal(BookingStatus.Expired, await GetStatusAsync(id));
        Assert.Equal(BookingStatus.Confirmed, await GetStatusAsync(confirmedId));
    }

    [Fact]
    public async Task ExpirePendingBookingsAsync_respects_CancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => bookingService.ExpirePendingBookingsAsync(cts.Token));
    }

    [Fact]
    public async Task BackgroundService_stops_when_cancelled()
    {
        var service = new PendingBookingExpirationService(
            _fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BookingSettings
            {
                PendingExpirationMinutes = 30,
                ExpirationPollingIntervalMinutes = 1
            }),
            NullLogger<PendingBookingExpirationService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    private async Task<int> ExpireAsync()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        return await bookingService.ExpirePendingBookingsAsync();
    }

    private async Task<BookingStatus> GetStatusAsync(int bookingId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => b.Status)
            .SingleAsync();
    }

    private async Task<int> SeedBookingAsync(
        SetupContext setup,
        BookingStatus status,
        DateTime createdAt,
        int startOffsetMinutes = 0)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = new Customer
        {
            BusinessId = setup.BusinessId,
            Name = $"Expire {status} {Guid.NewGuid():N}",
            PhoneNumber = $"09128{Random.Shared.Next(100000, 999999)}",
            UserId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var start = DateTime.UtcNow.AddDays(2).AddMinutes(startOffsetMinutes);
        var booking = new Booking
        {
            BusinessId = setup.BusinessId,
            CustomerId = customer.Id,
            ServiceId = setup.ServiceId,
            EmployeeId = setup.EmployeeId,
            StartTime = start,
            EndTime = start.AddMinutes(30),
            Status = status,
            CreatorType = BookingCreatorType.Guest,
            CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking.Id;
    }

    private async Task<SetupContext> CreateBookableSetupAsync(string ownerPhone)
    {
        var owner = await CreateOwnerWithBusinessAsync(ownerPhone, $"Shop {ownerPhone}");
        var employeePhone = ownerPhone.Replace("09128", "09129", StringComparison.Ordinal);
        var employee = await CreateEmployeeAsync(owner.AccessToken, owner.BusinessId, employeePhone);
        var service = await CreateServiceAsync(owner.AccessToken, owner.BusinessId, "Haircut");

        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{employee.Id}/services",
            owner.AccessToken,
            new { serviceId = service.Id });
        assign.EnsureSuccessStatusCode();

        return new SetupContext(owner.AccessToken, owner.BusinessId, employee.Id, service.Id);
    }

    private async Task<OwnerContext> CreateOwnerWithBusinessAsync(string phoneNumber, string businessName)
    {
        var customer = await RegisterCustomerAsync(phoneNumber);
        var registerResponse = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/Business/register",
            customer.AccessToken,
            new
            {
                name = businessName,
                city = "Tehran",
                address = "Owner Address",
                description = "Owner business"
            });
        registerResponse.EnsureSuccessStatusCode();
        var auth = await ReadAsync<AuthResponse>(registerResponse);

        var mine = await SendAuthorizedAsync(HttpMethod.Get, "/api/Business/mine", auth.AccessToken);
        mine.EnsureSuccessStatusCode();
        var myBusiness = await ReadAsync<MyBusinessResponse>(mine);
        Assert.True(myBusiness.HasBusiness);
        Assert.NotNull(myBusiness.Business);

        return new OwnerContext(auth.AccessToken, myBusiness.Business!.Id);
    }

    private async Task<EmployeeDto> CreateEmployeeAsync(
        string ownerToken,
        int businessId,
        string employeePhone)
    {
        await RegisterCustomerAsync(employeePhone);

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/business/{businessId}/employees",
            ownerToken,
            new { phoneNumber = employeePhone });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<EmployeeDto>(response);
    }

    private async Task<ServiceDto> CreateServiceAsync(
        string ownerToken,
        int businessId,
        string title)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/business/{businessId}/services",
            ownerToken,
            new
            {
                title,
                price = 100000m,
                durationMinutes = 30
            });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<ServiceDto>(response);
    }

    private async Task<AuthResponse> RegisterCustomerAsync(string phoneNumber)
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/register", new
        {
            name = $"User {phoneNumber}",
            phoneNumber,
            password = "Password1",
            confirmPassword = "Password1"
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<AuthResponse>(response);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string url,
        string accessToken,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await _client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }

    private sealed record AuthResponse(UserResponse User, string AccessToken, string RefreshToken);
    private sealed record UserResponse(string UserId, string Name, string PhoneNumber);
    private sealed record MyBusinessResponse(bool HasBusiness, BusinessDto? Business);
    private sealed record BusinessDto(
        int Id,
        string Name,
        string City,
        string Address,
        string? Description,
        string? ProfileImageUrl,
        string OwnerId);
    private sealed record EmployeeDto(int Id, string UserId, string FullName, string PhoneNumber, int BusinessId);
    private sealed record ServiceDto(int Id, string Title, decimal Price, int DurationMinutes, int BusinessId);
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(string OwnerToken, int BusinessId, int EmployeeId, int ServiceId);
}
