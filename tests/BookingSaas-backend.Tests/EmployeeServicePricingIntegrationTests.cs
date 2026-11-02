using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingSaas_backend.Tests;

/// <summary>
/// Phase 5 end-to-end chain: assign → availability → override → book → snapshot stability.
/// </summary>
[Collection(AuthCollection.Name)]
public sealed class EmployeeServicePricingIntegrationTests : IAsyncLifetime
{
    private const decimal DefaultPrice = 100000m;
    private const int DefaultDurationMinutes = 30;
    private const decimal OverridePrice = 275000m;
    private const int OverrideDurationMinutes = 45;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public EmployeeServicePricingIntegrationTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Full_flow_assign_availability_override_bookings_and_price_stability()
    {
        var date = FutureLocalDate(daysAhead: 40);

        // 1–2. Owner, service defaults, employee, assignment without overrides, working hours.
        var owner = await CreateOwnerWithBusinessAsync("09129000001", "E2E Shop");
        var employee = await CreateEmployeeAsync(owner.AccessToken, owner.BusinessId, "09129100001");
        var service = await CreateServiceAsync(owner.AccessToken, owner.BusinessId, "E2E Cut");

        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{employee.Id}/services",
            owner.AccessToken,
            new { serviceId = service.Id });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
        var assigned = await ReadAsync<EmployeeServiceDto>(assign);
        Assert.Null(assigned.PriceOverride);
        Assert.Null(assigned.DurationMinutesOverride);
        Assert.Equal(DefaultPrice, assigned.EffectivePrice);
        Assert.Equal(DefaultDurationMinutes, assigned.EffectiveDurationMinutes);

        var hours = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/business/{owner.BusinessId}/employees/{employee.Id}/working-hours",
            owner.AccessToken,
            new
            {
                date = date.ToString("yyyy-MM-dd"),
                startTime = "09:00:00",
                endTime = "12:00:00"
            });
        hours.EnsureSuccessStatusCode();

        // 3. Availability uses Service default duration (30).
        var defaultSlots = await GetAvailabilityAsync(owner.BusinessId, service.Id, employee.Id, date);
        Assert.NotEmpty(defaultSlots);
        Assert.All(defaultSlots, s => Assert.Equal(TimeSpan.FromMinutes(30), s.EndTime - s.StartTime));

        // 4. Update EmployeeService with custom price + duration.
        var put = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/employees/{employee.Id}/services/{service.Id}",
            owner.AccessToken,
            new
            {
                priceOverride = OverridePrice,
                durationMinutesOverride = OverrideDurationMinutes
            });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await ReadAsync<EmployeeServiceDto>(put);
        Assert.Equal(OverridePrice, updated.EffectivePrice);
        Assert.Equal(OverrideDurationMinutes, updated.EffectiveDurationMinutes);
        Assert.Equal(DefaultPrice, updated.DefaultPrice);
        Assert.Equal(DefaultDurationMinutes, updated.DefaultDurationMinutes);

        // 5. Availability uses custom duration (45).
        var overrideSlots = await GetAvailabilityAsync(owner.BusinessId, service.Id, employee.Id, date);
        Assert.NotEmpty(overrideSlots);
        Assert.All(overrideSlots, s => Assert.Equal(TimeSpan.FromMinutes(45), s.EndTime - s.StartTime));
        Assert.DoesNotContain(overrideSlots, s =>
            s.StartTime.TimeOfDay == TimeSpan.FromHours(9) &&
            s.EndTime.TimeOfDay == TimeSpan.FromHours(9.5));

        var slotStart = overrideSlots[0].StartTime.UtcDateTime;

        // 6–7. Authenticated booking stores effective price + EndTime.
        var customer = await RegisterCustomerAsync("09129200001");
        var authResponse = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/bookings",
            customer.AccessToken,
            new
            {
                businessId = owner.BusinessId,
                serviceId = service.Id,
                employeeId = employee.Id,
                startTime = slotStart,
                endTime = slotStart.AddMinutes(30)
            });
        Assert.Equal(HttpStatusCode.Created, authResponse.StatusCode);
        var authBooking = await ReadAsync<BookingDto>(authResponse);
        Assert.Equal(OverridePrice, authBooking.Price);
        Assert.Equal(slotStart.AddMinutes(OverrideDurationMinutes), authBooking.EndTime);

        // 8–9. Guest booking on a later free slot with same effective values.
        var guestSlot = overrideSlots.First(s => s.StartTime.UtcDateTime >= authBooking.EndTime);
        var guestResponse = await _client.PostAsJsonAsync("/api/bookings/guest", new
        {
            businessId = owner.BusinessId,
            serviceId = service.Id,
            employeeId = employee.Id,
            startTime = guestSlot.StartTime.UtcDateTime,
            endTime = guestSlot.StartTime.UtcDateTime.AddMinutes(30),
            name = "E2E Guest",
            phoneNumber = "09129300001"
        });
        Assert.Equal(HttpStatusCode.Created, guestResponse.StatusCode);
        var guestBooking = await ReadAsync<BookingDto>(guestResponse);
        Assert.Equal(OverridePrice, guestBooking.Price);
        Assert.Equal(
            guestSlot.StartTime.UtcDateTime.AddMinutes(OverrideDurationMinutes),
            guestBooking.EndTime);

        // 10–11. Change Service + overrides; prior bookings keep snapshots.
        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var svc = await db.Services.SingleAsync(s => s.Id == service.Id);
            svc.Price = 999999m;
            svc.DurationMinutes = 15;

            var assignment = await db.EmployeeServices.SingleAsync(es =>
                es.EmployeesId == employee.Id &&
                es.ServicesId == service.Id);
            assignment.PriceOverride = 111111m;
            assignment.DurationMinutesOverride = 60;
            await db.SaveChangesAsync();
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persistedAuth = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == authBooking.Id);
            var persistedGuest = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == guestBooking.Id);
            Assert.Equal(OverridePrice, persistedAuth.Price);
            Assert.Equal(OverridePrice, persistedGuest.Price);
            Assert.Equal(authBooking.EndTime, persistedAuth.EndTime, TimeSpan.FromSeconds(1));
            Assert.Equal(guestBooking.EndTime, persistedGuest.EndTime, TimeSpan.FromSeconds(1));
        }

        var detail = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings/{authBooking.Id}",
            owner.AccessToken);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var details = await ReadAsync<BookingDetailsDto>(detail);
        Assert.Equal(OverridePrice, details.Service.Price);
    }

    [Fact]
    public async Task Put_omitted_field_preserves_other_override_while_null_resets()
    {
        var owner = await CreateOwnerWithBusinessAsync("09129000002", "Omit Null Shop");
        var employee = await CreateEmployeeAsync(owner.AccessToken, owner.BusinessId, "09129100002");
        var service = await CreateServiceAsync(owner.AccessToken, owner.BusinessId, "Omit Null Cut");

        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{employee.Id}/services",
            owner.AccessToken,
            new
            {
                serviceId = service.Id,
                priceOverride = 150000m,
                durationMinutesOverride = 40
            });
        assign.EnsureSuccessStatusCode();

        var priceOnly = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/employees/{employee.Id}/services/{service.Id}",
            owner.AccessToken,
            new { priceOverride = 160000m });
        Assert.Equal(HttpStatusCode.OK, priceOnly.StatusCode);
        var afterPrice = await ReadAsync<EmployeeServiceDto>(priceOnly);
        Assert.Equal(160000m, afterPrice.PriceOverride);
        Assert.Equal(40, afterPrice.DurationMinutesOverride);

        var resetPrice = await SendAuthorizedRawAsync(
            HttpMethod.Put,
            $"/api/employees/{employee.Id}/services/{service.Id}",
            owner.AccessToken,
            """{"priceOverride":null}""");
        Assert.Equal(HttpStatusCode.OK, resetPrice.StatusCode);
        var afterReset = await ReadAsync<EmployeeServiceDto>(resetPrice);
        Assert.Null(afterReset.PriceOverride);
        Assert.Equal(40, afterReset.DurationMinutesOverride);
        Assert.Equal(DefaultPrice, afterReset.EffectivePrice);
        Assert.Equal(40, afterReset.EffectiveDurationMinutes);
    }

    private async Task<IReadOnlyList<SlotDto>> GetAvailabilityAsync(
        int businessId,
        int serviceId,
        int employeeId,
        DateOnly date)
    {
        var response = await _client.GetAsync(
            $"/api/bookings/availability?businessId={businessId}&serviceId={serviceId}&employeeId={employeeId}&date={date:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<List<SlotDto>>(response);
    }

    private static DateOnly FutureLocalDate(int daysAhead)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return DateOnly.FromDateTime(localNow.Date.AddDays(daysAhead));
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
                price = DefaultPrice,
                durationMinutes = DefaultDurationMinutes
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

    private async Task<HttpResponseMessage> SendAuthorizedRawAsync(
        HttpMethod method,
        string url,
        string accessToken,
        string jsonBody)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
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
    private sealed record EmployeeServiceDto(
        int Id,
        string Title,
        decimal DefaultPrice,
        int DefaultDurationMinutes,
        decimal? PriceOverride,
        int? DurationMinutesOverride,
        decimal EffectivePrice,
        int EffectiveDurationMinutes,
        int BusinessId);
    private sealed record SlotDto(DateTimeOffset StartTime, DateTimeOffset EndTime);
    private sealed record BookingDto(
        int Id,
        int BusinessId,
        int CustomerId,
        int ServiceId,
        int EmployeeId,
        DateTime StartTime,
        DateTime EndTime,
        string? Notes,
        BookingStatus Status,
        BookingCreatorType CreatorType,
        string? CreatedByUserId,
        DateTime CreatedAt,
        decimal? Price);
    private sealed record BookingDetailsDto(
        int Id,
        int BusinessId,
        DateTime StartTime,
        DateTime EndTime,
        BookingStatus Status,
        BookingCreatorType CreatorType,
        DateTime CreatedAt,
        string? Notes,
        object Customer,
        BookingDetailsServiceDto Service,
        object Employee,
        object Business);
    private sealed record BookingDetailsServiceDto(int Id, string Name, int DurationMinutes, decimal? Price);
    private sealed record OwnerContext(string AccessToken, int BusinessId);
}
