using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Xunit;

namespace BookingSaas_backend.Tests;

[Collection(AuthCollection.Name)]
public sealed class BookingDetailTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public BookingDetailTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Owner_can_retrieve_booking_from_own_business()
    {
        var setup = await CreateBookableSetupAsync("09128000001");
        var booking = await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(1), "Detail Cust", "09128100001");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, $"/api/bookings/{booking.Id}", setup.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await ReadAsync<BookingDetailsDto>(response);
        Assert.Equal(booking.Id, detail.Id);
        Assert.Equal(setup.BusinessId, detail.BusinessId);
        Assert.Equal("Detail Cust", detail.Customer.Name);
        Assert.Equal("09128100001", detail.Customer.PhoneNumber);
        Assert.Equal("Haircut", detail.Service.Name);
        Assert.Equal(30, detail.Service.DurationMinutes);
        Assert.Equal(100000m, detail.Service.Price);
        Assert.Equal(setup.EmployeeId, detail.Employee.Id);
        Assert.False(string.IsNullOrWhiteSpace(detail.Employee.Name));
        Assert.Equal(setup.BusinessId, detail.Business.Id);
        Assert.False(string.IsNullOrWhiteSpace(detail.Business.Name));
        Assert.Equal(BookingStatus.Pending, detail.Status);
        Assert.Equal(BookingCreatorType.Guest, detail.CreatorType);
    }

    [Fact]
    public async Task Owner_cannot_retrieve_another_business_booking()
    {
        var setupA = await CreateBookableSetupAsync("09128000002");
        var setupB = await CreateBookableSetupAsync("09128000003");
        var booking = await CreateGuestBookingAsync(
            setupA, DateTime.UtcNow.AddDays(1), "Only A", "09128100002");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, $"/api/bookings/{booking.Id}", setupB.OwnerToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Employee_can_retrieve_own_assigned_booking()
    {
        var setup = await CreateTwoEmployeeSetupAsync("09128000004");
        var booking = await CreateGuestBookingAsync(
            setup.Primary, DateTime.UtcNow.AddDays(1), "For Emp1", "09128100004");

        var empLogin = await LoginAsync(setup.PrimaryEmployeePhone);
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, $"/api/bookings/{booking.Id}", empLogin.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await ReadAsync<BookingDetailsDto>(response);
        Assert.Equal(booking.Id, detail.Id);
        Assert.Equal(setup.Primary.EmployeeId, detail.Employee.Id);
        Assert.Equal("For Emp1", detail.Customer.Name);
    }

    [Fact]
    public async Task Employee_cannot_retrieve_another_employee_booking()
    {
        var setup = await CreateTwoEmployeeSetupAsync("09128000005");
        var otherBooking = await CreateGuestBookingAsync(
            setup.Secondary, DateTime.UtcNow.AddDays(1), "For Emp2", "09128100005");

        var empLogin = await LoginAsync(setup.PrimaryEmployeePhone);
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, $"/api/bookings/{otherBooking.Id}", empLogin.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_access_booking_details()
    {
        var setup = await CreateBookableSetupAsync("09128000006");
        var booking = await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(1), "Cust Block", "09128100006");
        var customer = await RegisterCustomerAsync("09128200006");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, $"/api/bookings/{booking.Id}", customer.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_is_rejected()
    {
        var response = await _client.GetAsync("/api/bookings/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Nonexistent_booking_returns_not_found()
    {
        var setup = await CreateBookableSetupAsync("09128000007");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/999999", setup.OwnerToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("رزرو یافت نشد", body);
    }

    [Fact]
    public async Task Response_contains_expected_nested_fields()
    {
        var setup = await CreateBookableSetupAsync("09128000008");
        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var booking = await CreateGuestBookingAsync(
            setup, start, "Nested Fields", "09128100008");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, $"/api/bookings/{booking.Id}", setup.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("businessId", out _));
        Assert.True(root.TryGetProperty("startTime", out _));
        Assert.True(root.TryGetProperty("endTime", out _));
        Assert.True(root.TryGetProperty("status", out _));
        Assert.True(root.TryGetProperty("creatorType", out _));
        Assert.True(root.TryGetProperty("createdAt", out _));
        Assert.True(root.TryGetProperty("notes", out _));

        Assert.True(root.TryGetProperty("customer", out var customer));
        Assert.True(customer.TryGetProperty("id", out _));
        Assert.True(customer.TryGetProperty("name", out _));
        Assert.True(customer.TryGetProperty("phoneNumber", out _));

        Assert.True(root.TryGetProperty("service", out var service));
        Assert.True(service.TryGetProperty("id", out _));
        Assert.True(service.TryGetProperty("name", out _));
        Assert.True(service.TryGetProperty("durationMinutes", out _));
        Assert.True(service.TryGetProperty("price", out _));

        Assert.True(root.TryGetProperty("employee", out var employee));
        Assert.True(employee.TryGetProperty("id", out _));
        Assert.True(employee.TryGetProperty("name", out _));
        Assert.True(employee.TryGetProperty("profileImageUrl", out _));

        Assert.True(root.TryGetProperty("business", out var business));
        Assert.True(business.TryGetProperty("id", out _));
        Assert.True(business.TryGetProperty("name", out _));

        var detail = JsonSerializer.Deserialize<BookingDetailsDto>(payload, JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(booking.CustomerId, detail!.Customer.Id);
        Assert.Equal(booking.ServiceId, detail.Service.Id);
        Assert.Equal(booking.EmployeeId, detail.Employee.Id);
        Assert.Equal(booking.BusinessId, detail.Business.Id);
        Assert.Equal(start, detail.StartTime, TimeSpan.FromSeconds(1));
        Assert.Equal(start.AddMinutes(30), detail.EndTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Response_does_not_expose_sensitive_identity_information()
    {
        var setup = await CreateBookableSetupAsync("09128000009");
        var booking = await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(1), "Safe Fields", "09128100009");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, $"/api/bookings/{booking.Id}", setup.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var json = doc.RootElement.GetRawText();

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concurrencyStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("normalizedUserName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.False(doc.RootElement.TryGetProperty("createdByUserId", out _));
        Assert.False(doc.RootElement.TryGetProperty("userId", out _));
        Assert.False(doc.RootElement.TryGetProperty("ownerId", out _));
    }

    private async Task<BookingDto> CreateGuestBookingAsync(
        SetupContext setup,
        DateTime startTime,
        string name,
        string phone)
    {
        var response = await _client.PostAsJsonAsync("/api/bookings/guest", new
        {
            businessId = setup.BusinessId,
            serviceId = setup.ServiceId,
            employeeId = setup.EmployeeId,
            startTime,
            endTime = startTime.AddMinutes(30),
            name,
            phoneNumber = phone
        });

        response.EnsureSuccessStatusCode();
        return await ReadAsync<BookingDto>(response);
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

        return new SetupContext(
            owner.AccessToken,
            owner.BusinessId,
            employee.Id,
            service.Id);
    }

    private async Task<TwoEmployeeSetup> CreateTwoEmployeeSetupAsync(string ownerPhone)
    {
        var primary = await CreateBookableSetupAsync(ownerPhone);
        var secondPhone = ownerPhone.Replace("09128", "09127", StringComparison.Ordinal);
        var secondEmployee = await CreateEmployeeAsync(
            primary.OwnerToken, primary.BusinessId, secondPhone);

        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{secondEmployee.Id}/services",
            primary.OwnerToken,
            new { serviceId = primary.ServiceId });
        assign.EnsureSuccessStatusCode();

        var secondary = primary with { EmployeeId = secondEmployee.Id };
        var primaryEmployeePhone = ownerPhone.Replace("09128", "09129", StringComparison.Ordinal);
        return new TwoEmployeeSetup(primary, secondary, primaryEmployeePhone, secondPhone);
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
    private sealed record BookingDetailsCustomerDto(int Id, string Name, string? PhoneNumber);
    private sealed record BookingDetailsServiceDto(int Id, string Name, int DurationMinutes, decimal Price);
    private sealed record BookingDetailsEmployeeDto(int Id, string Name, string? ProfileImageUrl);
    private sealed record BookingDetailsBusinessDto(int Id, string Name);
    private sealed record BookingDetailsDto(
        int Id,
        int BusinessId,
        DateTime StartTime,
        DateTime EndTime,
        BookingStatus Status,
        BookingCreatorType CreatorType,
        DateTime CreatedAt,
        string? Notes,
        BookingDetailsCustomerDto Customer,
        BookingDetailsServiceDto Service,
        BookingDetailsEmployeeDto Employee,
        BookingDetailsBusinessDto Business);
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(string OwnerToken, int BusinessId, int EmployeeId, int ServiceId);
    private sealed record TwoEmployeeSetup(
        SetupContext Primary,
        SetupContext Secondary,
        string PrimaryEmployeePhone,
        string SecondaryEmployeePhone);
}
