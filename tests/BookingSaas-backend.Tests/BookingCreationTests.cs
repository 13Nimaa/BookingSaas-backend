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
public sealed class BookingCreationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public BookingCreationTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Authenticated_customer_creates_valid_booking()
    {
        var setup = await CreateBookableSetupAsync("09124000001");
        var customer = await RegisterCustomerAsync("09124100001");
        var startTime = DateTime.UtcNow.AddDays(1).AddHours(2);

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
                endTime = startTime.AddHours(5)
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await ReadAsync<BookingDto>(response);
        Assert.Equal(setup.BusinessId, booking.BusinessId);
        Assert.Equal(setup.ServiceId, booking.ServiceId);
        Assert.Equal(setup.EmployeeId, booking.EmployeeId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(BookingCreatorType.Customer, booking.CreatorType);
        Assert.Equal(customer.User.UserId, booking.CreatedByUserId);
        Assert.Equal(startTime, booking.StartTime);
        Assert.Equal(startTime.AddMinutes(30), booking.EndTime);
    }

    [Fact]
    public async Task Guest_creates_valid_booking()
    {
        var setup = await CreateBookableSetupAsync("09124000002");
        var startTime = DateTime.UtcNow.AddDays(1).AddHours(3);

        var response = await _client.PostAsJsonAsync("/api/bookings/guest", new
        {
            businessId = setup.BusinessId,
            serviceId = setup.ServiceId,
            employeeId = setup.EmployeeId,
            startTime,
            endTime = startTime.AddMinutes(30),
            name = "Guest User",
            phoneNumber = "09124200002"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await ReadAsync<BookingDto>(response);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(BookingCreatorType.Guest, booking.CreatorType);
        Assert.Null(booking.CreatedByUserId);
        Assert.Equal(startTime.AddMinutes(30), booking.EndTime);
    }

    [Fact]
    public async Task Client_provided_end_time_is_ignored()
    {
        var setup = await CreateBookableSetupAsync("09124000003");
        var customer = await RegisterCustomerAsync("09124100003");
        var startTime = DateTime.UtcNow.AddDays(2);
        var wrongEnd = startTime.AddHours(8);

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
                endTime = wrongEnd
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await ReadAsync<BookingDto>(response);
        Assert.Equal(startTime.AddMinutes(30), booking.EndTime);
        Assert.NotEqual(wrongEnd, booking.EndTime);
    }

    [Fact]
    public async Task Employee_from_another_business_is_rejected()
    {
        var setupA = await CreateBookableSetupAsync("09124000004");
        var setupB = await CreateBookableSetupAsync("09124000005");
        var customer = await RegisterCustomerAsync("09124100004");
        var startTime = DateTime.UtcNow.AddDays(1);

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/bookings",
            customer.AccessToken,
            new
            {
                businessId = setupA.BusinessId,
                serviceId = setupA.ServiceId,
                employeeId = setupB.EmployeeId,
                startTime,
                endTime = startTime.AddMinutes(30)
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Service_from_another_business_is_rejected()
    {
        var setupA = await CreateBookableSetupAsync("09124000006");
        var setupB = await CreateBookableSetupAsync("09124000007");
        var customer = await RegisterCustomerAsync("09124100006");
        var startTime = DateTime.UtcNow.AddDays(1);

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/bookings",
            customer.AccessToken,
            new
            {
                businessId = setupA.BusinessId,
                serviceId = setupB.ServiceId,
                employeeId = setupA.EmployeeId,
                startTime,
                endTime = startTime.AddMinutes(30)
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Employee_not_assigned_to_service_is_rejected()
    {
        var owner = await CreateOwnerWithBusinessAsync("09124000008", "Unassigned Shop");
        var employee = await CreateEmployeeAsync(owner.AccessToken, owner.BusinessId, "09124300008");
        var service = await CreateServiceAsync(owner.AccessToken, owner.BusinessId, "Unassigned Cut");
        var customer = await RegisterCustomerAsync("09124100008");
        var startTime = DateTime.UtcNow.AddDays(1);

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/bookings",
            customer.AccessToken,
            new
            {
                businessId = owner.BusinessId,
                serviceId = service.Id,
                employeeId = employee.Id,
                startTime,
                endTime = startTime.AddMinutes(30)
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_access_to_authenticated_endpoint_is_rejected()
    {
        var setup = await CreateBookableSetupAsync("09124000009");
        var startTime = DateTime.UtcNow.AddDays(1);

        var response = await _client.PostAsJsonAsync("/api/bookings", new
        {
            businessId = setup.BusinessId,
            serviceId = setup.ServiceId,
            employeeId = setup.EmployeeId,
            startTime,
            endTime = startTime.AddMinutes(30)
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Creator_fields_are_persisted_correctly()
    {
        var setup = await CreateBookableSetupAsync("09124000010");
        var customer = await RegisterCustomerAsync("09124100010");
        var startTime = DateTime.UtcNow.AddDays(3);

        var authResponse = await SendAuthorizedAsync(
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
        Assert.Equal(HttpStatusCode.Created, authResponse.StatusCode);
        var authBooking = await ReadAsync<BookingDto>(authResponse);

        var guestStart = startTime.AddHours(2);
        var guestResponse = await _client.PostAsJsonAsync("/api/bookings/guest", new
        {
            businessId = setup.BusinessId,
            serviceId = setup.ServiceId,
            employeeId = setup.EmployeeId,
            startTime = guestStart,
            endTime = guestStart.AddMinutes(30),
            name = "Guest Creator",
            phoneNumber = "09124200010"
        });
        Assert.Equal(HttpStatusCode.Created, guestResponse.StatusCode);
        var guestBooking = await ReadAsync<BookingDto>(guestResponse);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var persistedAuth = await db.Bookings.AsNoTracking()
            .SingleAsync(b => b.Id == authBooking.Id);
        Assert.Equal(BookingCreatorType.Customer, persistedAuth.CreatorType);
        Assert.Equal(customer.User.UserId, persistedAuth.CreatedByUserId);

        var persistedGuest = await db.Bookings.AsNoTracking()
            .SingleAsync(b => b.Id == guestBooking.Id);
        Assert.Equal(BookingCreatorType.Guest, persistedGuest.CreatorType);
        Assert.Null(persistedGuest.CreatedByUserId);
    }

    [Fact]
    public async Task Guest_with_matching_registered_customer_phone_is_not_linked()
    {
        var setup = await CreateBookableSetupAsync("09124000011");
        var sharedPhone = "09124900011";
        var registered = await RegisterCustomerAsync(sharedPhone);
        var startTime = DateTime.UtcNow.AddDays(4);

        var authResponse = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/bookings",
            registered.AccessToken,
            new
            {
                businessId = setup.BusinessId,
                serviceId = setup.ServiceId,
                employeeId = setup.EmployeeId,
                startTime,
                endTime = startTime.AddMinutes(30)
            });
        Assert.Equal(HttpStatusCode.Created, authResponse.StatusCode);
        var authBooking = await ReadAsync<BookingDto>(authResponse);

        var guestStart = startTime.AddHours(1);
        var guestResponse = await _client.PostAsJsonAsync("/api/bookings/guest", new
        {
            businessId = setup.BusinessId,
            serviceId = setup.ServiceId,
            employeeId = setup.EmployeeId,
            startTime = guestStart,
            endTime = guestStart.AddMinutes(30),
            name = "Same Phone Guest",
            phoneNumber = sharedPhone
        });
        Assert.Equal(HttpStatusCode.Created, guestResponse.StatusCode);
        var guestBooking = await ReadAsync<BookingDto>(guestResponse);

        Assert.NotEqual(authBooking.CustomerId, guestBooking.CustomerId);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var guestCustomer = await db.Customers.AsNoTracking()
            .SingleAsync(c => c.Id == guestBooking.CustomerId);
        Assert.Null(guestCustomer.UserId);
        Assert.Equal(sharedPhone, guestCustomer.PhoneNumber);
    }

    private async Task<SetupContext> CreateBookableSetupAsync(string ownerPhone)
    {
        var owner = await CreateOwnerWithBusinessAsync(ownerPhone, $"Shop {ownerPhone}");
        var employeePhone = ownerPhone.Replace("09124", "09125", StringComparison.Ordinal);
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
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(string OwnerToken, int BusinessId, int EmployeeId, int ServiceId);
}
