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

[Collection(AuthCollection.Name)]
public sealed class BookingStatusManagementTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public BookingStatusManagementTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Owner_confirms_pending_booking()
    {
        var setup = await CreateBookableSetupAsync("09127000001");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setup.OwnerToken,
            new { status = BookingStatus.Confirmed });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<BookingDto>(response);
        Assert.Equal(BookingStatus.Confirmed, updated.Status);
        Assert.Equal(booking.Id, updated.Id);
        Assert.Equal(booking.BusinessId, updated.BusinessId);
        Assert.Equal(booking.CustomerId, updated.CustomerId);
        Assert.Equal(booking.ServiceId, updated.ServiceId);
        Assert.Equal(booking.EmployeeId, updated.EmployeeId);
    }

    [Fact]
    public async Task Owner_cancels_pending_booking()
    {
        var setup = await CreateBookableSetupAsync("09127000002");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setup.OwnerToken,
            new { status = BookingStatus.Cancelled });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<BookingDto>(response);
        Assert.Equal(BookingStatus.Cancelled, updated.Status);
    }

    [Fact]
    public async Task Owner_completes_confirmed_booking()
    {
        var setup = await CreateBookableSetupAsync("09127000003");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));
        await SetStatusDirectAsync(booking.Id, BookingStatus.Confirmed);

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setup.OwnerToken,
            new { status = BookingStatus.Completed });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<BookingDto>(response);
        Assert.Equal(BookingStatus.Completed, updated.Status);
    }

    [Fact]
    public async Task Owner_cancels_confirmed_booking()
    {
        var setup = await CreateBookableSetupAsync("09127000004");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));
        await SetStatusDirectAsync(booking.Id, BookingStatus.Confirmed);

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setup.OwnerToken,
            new { status = BookingStatus.Cancelled });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<BookingDto>(response);
        Assert.Equal(BookingStatus.Cancelled, updated.Status);
    }

    [Fact]
    public async Task Owner_cannot_modify_booking_from_another_business()
    {
        var setupA = await CreateBookableSetupAsync("09127000005");
        var setupB = await CreateBookableSetupAsync("09127000006");
        var booking = await CreatePendingBookingAsync(setupA, DateTime.UtcNow.AddDays(1));

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setupB.OwnerToken,
            new { status = BookingStatus.Confirmed });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Employee_cannot_modify_booking_status()
    {
        var setup = await CreateBookableSetupAsync("09127000007");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));
        var employeeLogin = await LoginAsync("09128000007");

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            employeeLogin.AccessToken,
            new { status = BookingStatus.Confirmed });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_modify_booking_status()
    {
        var setup = await CreateBookableSetupAsync("09127000008");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));
        var customer = await RegisterCustomerAsync("09127100008");

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            customer.AccessToken,
            new { status = BookingStatus.Confirmed });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancelled_booking_cannot_be_modified()
    {
        var setup = await CreateBookableSetupAsync("09127000009");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));
        await SetStatusDirectAsync(booking.Id, BookingStatus.Cancelled);

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setup.OwnerToken,
            new { status = BookingStatus.Confirmed });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Completed_booking_cannot_be_modified()
    {
        var setup = await CreateBookableSetupAsync("09127000010");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));
        await SetStatusDirectAsync(booking.Id, BookingStatus.Completed);

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setup.OwnerToken,
            new { status = BookingStatus.Cancelled });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Expired_booking_cannot_be_modified()
    {
        var setup = await CreateBookableSetupAsync("09127000011");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));
        await SetStatusDirectAsync(booking.Id, BookingStatus.Expired);

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setup.OwnerToken,
            new { status = BookingStatus.Confirmed });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pending_to_completed_is_rejected()
    {
        var setup = await CreateBookableSetupAsync("09127000012");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setup.OwnerToken,
            new { status = BookingStatus.Completed });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Confirmed_to_pending_is_rejected()
    {
        var setup = await CreateBookableSetupAsync("09127000013");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));
        await SetStatusDirectAsync(booking.Id, BookingStatus.Confirmed);

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status",
            setup.OwnerToken,
            new { status = BookingStatus.Pending });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Undefined_status_value_is_rejected()
    {
        var setup = await CreateBookableSetupAsync("09127000014");
        var booking = await CreatePendingBookingAsync(setup, DateTime.UtcNow.AddDays(1));

        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/bookings/{booking.Id}/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setup.OwnerToken);
        request.Content = new StringContent(
            """{"status":99}""",
            Encoding.UTF8,
            "application/json");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Nonexistent_booking_returns_not_found()
    {
        var setup = await CreateBookableSetupAsync("09127000015");

        var response = await SendAuthorizedAsync(
            HttpMethod.Patch,
            "/api/bookings/999999/status",
            setup.OwnerToken,
            new { status = BookingStatus.Confirmed });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<BookingDto> CreatePendingBookingAsync(
        SetupContext setup,
        DateTime startTime,
        string? customerPhone = null)
    {
        var phone = customerPhone
            ?? $"09129{setup.BusinessId:D5}{startTime.Minute:D2}";
        var customer = await RegisterCustomerAsync(phone);

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/bookings",
            customer.AccessToken,
            new
            {
                businessId = setup.BusinessId,
                serviceId = setup.ServiceId,
                employeeId = setup.EmployeeId,
                startTime,
                endTime = startTime.AddMinutes(30)
            });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<BookingDto>(response);
    }

    private async Task SetStatusDirectAsync(int bookingId, BookingStatus status)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<SetupContext> CreateBookableSetupAsync(string ownerPhone)
    {
        var owner = await CreateOwnerWithBusinessAsync(ownerPhone, $"Shop {ownerPhone}");
        var employeePhone = ownerPhone.Replace("09127", "09128", StringComparison.Ordinal);
        var employee = await CreateEmployeeAsync(owner.AccessToken, owner.BusinessId, employeePhone);
        var service = await CreateServiceAsync(owner.AccessToken, owner.BusinessId, "Haircut");

        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{employee.Id}/services",
            owner.AccessToken,
            new { serviceId = service.Id });
        assign.EnsureSuccessStatusCode();

        return new SetupContext(
            owner.AccessToken,
            owner.BusinessId,
            employee.Id,
            service.Id);
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

    private async Task<AuthResponse> LoginAsync(string phoneNumber)
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            phoneNumber,
            password = "Password1"
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
        DateTime CreatedAt);
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(string OwnerToken, int BusinessId, int EmployeeId, int ServiceId);
}
