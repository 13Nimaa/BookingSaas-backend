using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingSaas_backend.Tests;

[Collection(AuthCollection.Name)]
public sealed class BookingAvailabilityTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public BookingAvailabilityTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Returns_slots_within_working_hours()
    {
        var date = FutureLocalDate(daysAhead: 14);
        var setup = await CreateAvailabilitySetupAsync("09126000001", date, "09:00:00", "11:00:00");

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.NotEmpty(slots);
        Assert.All(slots, s =>
        {
            Assert.True(s.StartTime.TimeOfDay >= TimeSpan.FromHours(9));
            Assert.True(s.EndTime.TimeOfDay <= TimeSpan.FromHours(11));
        });
        Assert.Contains(slots, s =>
            s.StartTime.TimeOfDay == TimeSpan.FromHours(9) &&
            s.EndTime.TimeOfDay == TimeSpan.FromHours(9.5));
    }

    [Fact]
    public async Task Does_not_return_slots_that_exceed_working_interval()
    {
        var date = FutureLocalDate(daysAhead: 15);
        var setup = await CreateAvailabilitySetupAsync("09126000002", date, "09:00:00", "10:00:00");

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.True(s.EndTime.TimeOfDay <= TimeSpan.FromHours(10)));
        Assert.DoesNotContain(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9.75));
        Assert.Equal(TimeSpan.FromHours(9.5), slots[^1].StartTime.TimeOfDay);
    }

    [Fact]
    public async Task Excludes_pending_bookings()
    {
        var date = FutureLocalDate(daysAhead: 16);
        var setup = await CreateAvailabilitySetupAsync("09126000003", date, "09:00:00", "11:00:00");
        await SeedBookingAsync(setup, date, new TimeOnly(9, 0), BookingStatus.Pending);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.DoesNotContain(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9));
        Assert.Contains(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9.5));
    }

    [Fact]
    public async Task Excludes_confirmed_bookings()
    {
        var date = FutureLocalDate(daysAhead: 17);
        var setup = await CreateAvailabilitySetupAsync("09126000004", date, "09:00:00", "11:00:00");
        await SeedBookingAsync(setup, date, new TimeOnly(9, 30), BookingStatus.Confirmed);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.DoesNotContain(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9.5));
        Assert.Contains(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9));
    }

    [Fact]
    public async Task Does_not_exclude_time_occupied_only_by_cancelled_bookings()
    {
        var date = FutureLocalDate(daysAhead: 18);
        var setup = await CreateAvailabilitySetupAsync("09126000005", date, "09:00:00", "11:00:00");
        await SeedBookingAsync(setup, date, new TimeOnly(9, 0), BookingStatus.Cancelled);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.Contains(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9));
    }

    [Fact]
    public async Task Does_not_exclude_time_occupied_only_by_expired_bookings()
    {
        var date = FutureLocalDate(daysAhead: 19);
        var setup = await CreateAvailabilitySetupAsync("09126000006", date, "09:00:00", "11:00:00");
        await SeedBookingAsync(setup, date, new TimeOnly(9, 0), BookingStatus.Expired);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.Contains(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9));
    }

    [Fact]
    public async Task Does_not_exclude_time_occupied_only_by_completed_bookings()
    {
        var date = FutureLocalDate(daysAhead: 25);
        var setup = await CreateAvailabilitySetupAsync("09126000014", date, "09:00:00", "11:00:00");
        await SeedBookingAsync(setup, date, new TimeOnly(9, 0), BookingStatus.Completed);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.Contains(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9));
    }

    [Fact]
    public async Task Uses_service_duration_correctly()
    {
        var date = FutureLocalDate(daysAhead: 20);
        var setup = await CreateAvailabilitySetupAsync(
            "09126000007",
            date,
            "09:00:00",
            "10:00:00",
            durationMinutes: 45);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromMinutes(45), s.EndTime - s.StartTime));
        Assert.Equal(TimeSpan.FromHours(9), slots[0].StartTime.TimeOfDay);
        Assert.DoesNotContain(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9.5));
    }

    [Fact]
    public async Task Uses_default_service_duration_when_employee_has_no_override()
    {
        var date = FutureLocalDate(daysAhead: 26);
        var setup = await CreateAvailabilitySetupAsync(
            "09126000015",
            date,
            "09:00:00",
            "11:00:00",
            durationMinutes: 30);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromMinutes(30), s.EndTime - s.StartTime));
        Assert.Contains(slots, s =>
            s.StartTime.TimeOfDay == TimeSpan.FromHours(9) &&
            s.EndTime.TimeOfDay == TimeSpan.FromHours(9.5));
    }

    [Fact]
    public async Task Uses_employee_duration_override_instead_of_service_default()
    {
        var date = FutureLocalDate(daysAhead: 27);
        var setup = await CreateAvailabilitySetupAsync(
            "09126000016",
            date,
            "09:00:00",
            "12:00:00",
            durationMinutes: 30,
            durationMinutesOverride: 45);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromMinutes(45), s.EndTime - s.StartTime));
        Assert.Contains(slots, s =>
            s.StartTime.TimeOfDay == TimeSpan.FromHours(9) &&
            s.EndTime.TimeOfDay == TimeSpan.FromHours(9.75));
        Assert.DoesNotContain(slots, s =>
            s.StartTime.TimeOfDay == TimeSpan.FromHours(9) &&
            s.EndTime.TimeOfDay == TimeSpan.FromHours(9.5));
    }

    [Fact]
    public async Task Duration_override_applies_when_price_override_is_null()
    {
        var date = FutureLocalDate(daysAhead: 28);
        var setup = await CreateAvailabilitySetupAsync(
            "09126000017",
            date,
            "09:00:00",
            "11:00:00",
            durationMinutes: 30,
            durationMinutesOverride: 45,
            priceOverride: null);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromMinutes(45), s.EndTime - s.StartTime));

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assignment = await db.EmployeeServices.AsNoTracking().SingleAsync(es =>
            es.EmployeesId == setup.EmployeeId &&
            es.ServicesId == setup.ServiceId);
        Assert.Null(assignment.PriceOverride);
        Assert.Equal(45, assignment.DurationMinutesOverride);
    }

    [Fact]
    public async Task Does_not_return_slots_that_exceed_working_hours_with_duration_override()
    {
        var date = FutureLocalDate(daysAhead: 29);
        var setup = await CreateAvailabilitySetupAsync(
            "09126000018",
            date,
            "09:00:00",
            "10:00:00",
            durationMinutes: 30,
            durationMinutesOverride: 45);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.NotEmpty(slots);
        Assert.All(slots, s =>
        {
            Assert.Equal(TimeSpan.FromMinutes(45), s.EndTime - s.StartTime);
            Assert.True(s.EndTime.TimeOfDay <= TimeSpan.FromHours(10));
        });
        Assert.Contains(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9));
        Assert.Contains(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9.25));
        Assert.DoesNotContain(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9.5));
    }

    [Fact]
    public async Task Existing_bookings_block_slots_using_stored_start_and_end_times()
    {
        var date = FutureLocalDate(daysAhead: 30);
        var setup = await CreateAvailabilitySetupAsync(
            "09126000019",
            date,
            "09:00:00",
            "12:00:00",
            durationMinutes: 30,
            durationMinutesOverride: 45);

        // Stored booking uses historical EndTime (30 min), not the current 45-min override.
        await SeedBookingAsync(
            setup,
            date,
            new TimeOnly(9, 0),
            BookingStatus.Confirmed,
            storedDurationMinutes: 30);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.DoesNotContain(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9));
        // A 45-minute candidate starting 09:15 overlaps stored [09:00, 09:30).
        Assert.DoesNotContain(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9.25));
        // A 45-minute candidate starting 09:30 starts at the stored end and is free.
        Assert.Contains(slots, s =>
            s.StartTime.TimeOfDay == TimeSpan.FromHours(9.5) &&
            s.EndTime.TimeOfDay == TimeSpan.FromHours(10.25));
    }

    [Fact]
    public async Task Rejects_employee_not_assigned_to_service()
    {
        var owner = await CreateOwnerWithBusinessAsync("09126000008", "Unassigned Avail");
        var employee = await CreateEmployeeAsync(owner.AccessToken, owner.BusinessId, "09126100008");
        var service = await CreateServiceAsync(owner.AccessToken, owner.BusinessId, "No Assign", 30);
        var date = FutureLocalDate(daysAhead: 21);

        var response = await _client.GetAsync(AvailabilityUrl(
            owner.BusinessId, service.Id, employee.Id, date));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_employee_or_service_belonging_to_another_business()
    {
        var setupA = await CreateAvailabilitySetupAsync("09126000009", FutureLocalDate(22), "09:00:00", "11:00:00");
        var setupB = await CreateAvailabilitySetupAsync("09126000010", FutureLocalDate(22), "09:00:00", "11:00:00");
        var date = FutureLocalDate(daysAhead: 22);

        var foreignEmployee = await _client.GetAsync(AvailabilityUrl(
            setupA.BusinessId, setupA.ServiceId, setupB.EmployeeId, date));
        var foreignService = await _client.GetAsync(AvailabilityUrl(
            setupA.BusinessId, setupB.ServiceId, setupA.EmployeeId, date));

        Assert.Equal(HttpStatusCode.Conflict, foreignEmployee.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, foreignService.StatusCode);
    }

    [Fact]
    public async Task Handles_business_timezone_conversion_correctly()
    {
        var date = FutureLocalDate(daysAhead: 23);
        var setup = await CreateAvailabilitySetupAsync("09126000011", date, "09:00:00", "10:00:00");

        var slots = await GetAvailabilityAsync(setup, date);
        var first = Assert.Single(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9));

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(9, 0)), DateTimeKind.Unspecified),
            tz);

        Assert.Equal(expectedUtc, first.StartTime.UtcDateTime);
        Assert.Equal(tz.GetUtcOffset(expectedUtc), first.StartTime.Offset);
    }

    [Fact]
    public async Task Does_not_return_past_slots()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var yesterdayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date.AddDays(-1);
        var date = DateOnly.FromDateTime(yesterdayLocal);
        var setup = await CreateAvailabilitySetupAsync("09126000012", date, "09:00:00", "17:00:00");

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Handles_adjacent_bookings_with_half_open_interval_semantics()
    {
        var date = FutureLocalDate(daysAhead: 24);
        var setup = await CreateAvailabilitySetupAsync("09126000013", date, "09:00:00", "11:00:00");
        await SeedBookingAsync(setup, date, new TimeOnly(9, 0), BookingStatus.Confirmed);

        var slots = await GetAvailabilityAsync(setup, date);

        Assert.DoesNotContain(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9));
        Assert.Contains(slots, s => s.StartTime.TimeOfDay == TimeSpan.FromHours(9.5));
    }

    private async Task<IReadOnlyList<SlotDto>> GetAvailabilityAsync(SetupContext setup, DateOnly date)
    {
        var response = await _client.GetAsync(AvailabilityUrl(
            setup.BusinessId, setup.ServiceId, setup.EmployeeId, date));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<List<SlotDto>>(response);
    }

    private static string AvailabilityUrl(int businessId, int serviceId, int employeeId, DateOnly date) =>
        $"/api/bookings/availability?businessId={businessId}&serviceId={serviceId}&employeeId={employeeId}&date={date:yyyy-MM-dd}";

    private static DateOnly FutureLocalDate(int daysAhead)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return DateOnly.FromDateTime(localNow.Date.AddDays(daysAhead));
    }

    private async Task<SetupContext> CreateAvailabilitySetupAsync(
        string ownerPhone,
        DateOnly date,
        string startTime,
        string endTime,
        int durationMinutes = 30,
        int? durationMinutesOverride = null,
        decimal? priceOverride = null)
    {
        var owner = await CreateOwnerWithBusinessAsync(ownerPhone, $"Avail {ownerPhone}");
        var employeePhone = ownerPhone.Replace("09126", "09127", StringComparison.Ordinal);
        var employee = await CreateEmployeeAsync(owner.AccessToken, owner.BusinessId, employeePhone);
        var service = await CreateServiceAsync(
            owner.AccessToken,
            owner.BusinessId,
            "Avail Service",
            durationMinutes);

        object assignBody = durationMinutesOverride is null && priceOverride is null
            ? new { serviceId = service.Id }
            : new
            {
                serviceId = service.Id,
                priceOverride,
                durationMinutesOverride
            };

        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{employee.Id}/services",
            owner.AccessToken,
            assignBody);
        assign.EnsureSuccessStatusCode();

        var hours = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/business/{owner.BusinessId}/employees/{employee.Id}/working-hours",
            owner.AccessToken,
            new
            {
                date = date.ToString("yyyy-MM-dd"),
                startTime,
                endTime
            });
        hours.EnsureSuccessStatusCode();

        return new SetupContext(
            owner.AccessToken,
            owner.BusinessId,
            employee.Id,
            service.Id,
            durationMinutesOverride ?? durationMinutes);
    }

    private async Task SeedBookingAsync(
        SetupContext setup,
        DateOnly date,
        TimeOnly localStart,
        BookingStatus status,
        int? storedDurationMinutes = null)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(date.ToDateTime(localStart), DateTimeKind.Unspecified),
            tz);
        var durationMinutes = storedDurationMinutes ?? setup.DurationMinutes;

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = new Customer
        {
            BusinessId = setup.BusinessId,
            Name = $"Seed {status}",
            PhoneNumber = $"09128{setup.EmployeeId:D5}{Random.Shared.Next(10, 99)}"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.Bookings.Add(new Booking
        {
            BusinessId = setup.BusinessId,
            CustomerId = customer.Id,
            ServiceId = setup.ServiceId,
            EmployeeId = setup.EmployeeId,
            StartTime = startUtc,
            EndTime = startUtc.AddMinutes(durationMinutes),
            Status = status,
            CreatorType = BookingCreatorType.Guest,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
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
        string title,
        int durationMinutes)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/business/{businessId}/services",
            ownerToken,
            new
            {
                title,
                price = 100000m,
                durationMinutes
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
    private sealed record SlotDto(DateTimeOffset StartTime, DateTimeOffset EndTime);
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(
        string OwnerToken,
        int BusinessId,
        int EmployeeId,
        int ServiceId,
        int DurationMinutes);
}
