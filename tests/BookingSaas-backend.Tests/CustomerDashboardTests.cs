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
public sealed class CustomerDashboardTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public CustomerDashboardTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Empty_customer_gets_zero_dashboard()
    {
        var customer = await RegisterCustomerAsync("09129100001");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/customer/dashboard", customer.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dash = await ReadAsync<CustomerDashboardDto>(response);
        Assert.Equal(0, dash.Summary.UpcomingBookingsCount);
        Assert.Equal(0, dash.Summary.TotalBookingsCount);
        Assert.Equal(0, dash.Summary.FavoriteSalonsCount);
        Assert.Equal(0, dash.UpcomingBookings.TotalCount);
        Assert.Empty(dash.UpcomingBookings.Items);
        Assert.Empty(dash.RecentBookings);
    }

    [Fact]
    public async Task Dashboard_counts_and_lists_own_bookings_only()
    {
        var setup = await CreateBookableSetupAsync("09129100002", "Dash Salon");
        var customerA = await RegisterCustomerAsync("09129200002");
        var customerB = await RegisterCustomerAsync("09129210002");

        var upcoming = await CreateUserBookingAsync(
            setup, customerA.AccessToken, DateTime.UtcNow.AddDays(2));
        var past = await CreateUserBookingAsync(
            setup, customerA.AccessToken, DateTime.UtcNow.AddDays(3));
        await SetStatusDirectAsync(past.Id, BookingStatus.Completed);
        await SetStartTimeDirectAsync(past.Id, DateTime.UtcNow.AddDays(-2));

        await CreateUserBookingAsync(
            setup, customerB.AccessToken, DateTime.UtcNow.AddDays(1));

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/customer/dashboard", customerA.AccessToken);

        var dash = await ReadAsync<CustomerDashboardDto>(response);
        Assert.Equal(1, dash.Summary.UpcomingBookingsCount);
        Assert.Equal(2, dash.Summary.TotalBookingsCount);
        Assert.Equal(0, dash.Summary.FavoriteSalonsCount);
        Assert.Single(dash.UpcomingBookings.Items);
        Assert.Equal(upcoming.Id, dash.UpcomingBookings.Items[0].Id);
        Assert.Equal("Dash Salon", dash.UpcomingBookings.Items[0].BusinessName);
        Assert.Equal(2, dash.RecentBookings.Count);
        Assert.DoesNotContain(dash.RecentBookings, b => b.Id != upcoming.Id && b.Id != past.Id);
    }

    [Fact]
    public async Task Past_pending_is_not_upcoming()
    {
        var setup = await CreateBookableSetupAsync("09129100003", "Past Shop");
        var customer = await RegisterCustomerAsync("09129200003");
        var booking = await CreateUserBookingAsync(
            setup, customer.AccessToken, DateTime.UtcNow.AddDays(1));
        await SetStartTimeDirectAsync(booking.Id, DateTime.UtcNow.AddHours(-3));

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/customer/dashboard", customer.AccessToken);

        var dash = await ReadAsync<CustomerDashboardDto>(response);
        Assert.Equal(0, dash.Summary.UpcomingBookingsCount);
        Assert.Equal(1, dash.Summary.TotalBookingsCount);
        Assert.Empty(dash.UpcomingBookings.Items);
        Assert.Single(dash.RecentBookings);
    }

    [Fact]
    public async Task Favorite_add_list_remove_and_dashboard_count()
    {
        var setup = await CreateBookableSetupAsync("09129100004", "Fav Salon");
        var customer = await RegisterCustomerAsync("09129200004");

        var add = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/customer/favorites",
            customer.AccessToken,
            new { businessId = setup.BusinessId });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var fav = await ReadAsync<BusinessFavoriteDto>(add);
        Assert.Equal(setup.BusinessId, fav.BusinessId);
        Assert.Equal("Fav Salon", fav.Name);

        var addAgain = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/customer/favorites",
            customer.AccessToken,
            new { businessId = setup.BusinessId });
        Assert.Equal(HttpStatusCode.OK, addAgain.StatusCode);

        var list = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/customer/favorites", customer.AccessToken);
        var favorites = await ReadAsync<List<BusinessFavoriteDto>>(list);
        Assert.Single(favorites);

        var dash = await ReadAsync<CustomerDashboardDto>(
            await SendAuthorizedAsync(
                HttpMethod.Get, "/api/customer/dashboard", customer.AccessToken));
        Assert.Equal(1, dash.Summary.FavoriteSalonsCount);

        var remove = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/customer/favorites/{setup.BusinessId}",
            customer.AccessToken);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        dash = await ReadAsync<CustomerDashboardDto>(
            await SendAuthorizedAsync(
                HttpMethod.Get, "/api/customer/dashboard", customer.AccessToken));
        Assert.Equal(0, dash.Summary.FavoriteSalonsCount);
    }

    [Fact]
    public async Task Favorite_missing_business_returns_not_found()
    {
        var customer = await RegisterCustomerAsync("09129200005");
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/customer/favorites",
            customer.AccessToken,
            new { businessId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_can_cancel_own_pending_booking()
    {
        var setup = await CreateBookableSetupAsync("09129100006", "Cancel Shop");
        var customer = await RegisterCustomerAsync("09129200006");
        var booking = await CreateUserBookingAsync(
            setup, customer.AccessToken, DateTime.UtcNow.AddDays(1));

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/bookings/{booking.Id}/cancel",
            customer.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsync<BookingDto>(response);
        Assert.Equal(BookingStatus.Cancelled, dto.Status);
    }

    [Fact]
    public async Task Customer_can_cancel_own_confirmed_booking()
    {
        var setup = await CreateBookableSetupAsync("09129100007", "Cancel Confirmed");
        var customer = await RegisterCustomerAsync("09129200007");
        var booking = await CreateUserBookingAsync(
            setup, customer.AccessToken, DateTime.UtcNow.AddDays(1));
        await SetStatusDirectAsync(booking.Id, BookingStatus.Confirmed);

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/bookings/{booking.Id}/cancel",
            customer.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await ReadAsync<BookingDto>(response);
        Assert.Equal(BookingStatus.Cancelled, dto.Status);
    }

    [Fact]
    public async Task Customer_cannot_cancel_another_users_booking()
    {
        var setup = await CreateBookableSetupAsync("09129100008", "Cancel Other");
        var customerA = await RegisterCustomerAsync("09129200008");
        var customerB = await RegisterCustomerAsync("09129210008");
        var booking = await CreateUserBookingAsync(
            setup, customerA.AccessToken, DateTime.UtcNow.AddDays(1));

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/bookings/{booking.Id}/cancel",
            customerB.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_cancel_completed_booking()
    {
        var setup = await CreateBookableSetupAsync("09129100009", "Cancel Done");
        var customer = await RegisterCustomerAsync("09129200009");
        var booking = await CreateUserBookingAsync(
            setup, customer.AccessToken, DateTime.UtcNow.AddDays(1));
        await SetStatusDirectAsync(booking.Id, BookingStatus.Completed);

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/bookings/{booking.Id}/cancel",
            customer.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_dashboard_is_rejected()
    {
        var response = await _client.GetAsync("/api/customer/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task SetStatusDirectAsync(int bookingId, BookingStatus status)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task SetStartTimeDirectAsync(int bookingId, DateTime startTime)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        var duration = booking.EndTime - booking.StartTime;
        booking.StartTime = startTime;
        booking.EndTime = startTime + duration;
        await db.SaveChangesAsync();
    }

    private async Task<BookingDto> CreateUserBookingAsync(
        SetupContext setup,
        string customerToken,
        DateTime startTime)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/bookings",
            customerToken,
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

    private async Task<SetupContext> CreateBookableSetupAsync(string ownerPhone, string businessName)
    {
        var owner = await CreateOwnerWithBusinessAsync(ownerPhone, businessName);
        // Owners use 091291…; employees use 091281… + same suffix.
        var employeePhone = "091281" + ownerPhone[^5..];

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
    private sealed record CustomerDashboardDto(
        CustomerDashboardSummaryDto Summary,
        CustomerDashboardUpcomingDto UpcomingBookings,
        List<CustomerDashboardBookingItemDto> RecentBookings);
    private sealed record CustomerDashboardSummaryDto(
        int UpcomingBookingsCount,
        int TotalBookingsCount,
        int FavoriteSalonsCount);
    private sealed record CustomerDashboardUpcomingDto(
        int TotalCount,
        List<CustomerDashboardBookingItemDto> Items);
    private sealed record CustomerDashboardBookingItemDto(
        int Id,
        DateTime StartTime,
        DateTime EndTime,
        BookingStatus Status,
        int BusinessId,
        string BusinessName,
        string? BusinessProfileImageUrl,
        string ServiceName,
        string EmployeeName);
    private sealed record BusinessFavoriteDto(
        int BusinessId,
        string Name,
        string City,
        string? ProfileImageUrl,
        DateTime FavoritedAt);
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(
        string OwnerToken,
        int BusinessId,
        int EmployeeId,
        int ServiceId);
}
