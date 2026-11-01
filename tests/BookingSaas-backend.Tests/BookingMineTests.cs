using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingSaas_backend.Tests;

[Collection(AuthCollection.Name)]
public sealed class BookingMineTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public BookingMineTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Customer_sees_own_bookings()
    {
        var setup = await CreateBookableSetupAsync("09128000001", "Mine Shop A");
        var customer = await RegisterCustomerAsync("09128100001");
        var booking = await CreateUserBookingAsync(
            setup, customer.AccessToken, DateTime.UtcNow.AddDays(1));

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine", customer.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(booking.Id, page.Items[0].Id);
        Assert.Equal(setup.BusinessId, page.Items[0].BusinessId);
        Assert.Equal("Mine Shop A", page.Items[0].BusinessName);
        Assert.Equal("Haircut", page.Items[0].ServiceName);
        Assert.Equal(30, page.Items[0].ServiceDurationMinutes);
        Assert.False(string.IsNullOrWhiteSpace(page.Items[0].EmployeeName));
        Assert.Equal(BookingStatus.Pending, page.Items[0].Status);
        Assert.Equal(BookingCreatorType.Customer, page.Items[0].CreatorType);
    }

    [Fact]
    public async Task Customer_does_not_see_another_customers_bookings()
    {
        var setup = await CreateBookableSetupAsync("09128000002", "Mine Shop B");
        var customerA = await RegisterCustomerAsync("09128100002");
        var customerB = await RegisterCustomerAsync("09128110002");
        await CreateUserBookingAsync(setup, customerA.AccessToken, DateTime.UtcNow.AddDays(1));
        var bookingB = await CreateUserBookingAsync(
            setup, customerB.AccessToken, DateTime.UtcNow.AddDays(2));

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine", customerB.AccessToken);

        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(bookingB.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task Customer_sees_bookings_across_multiple_businesses()
    {
        var setupA = await CreateBookableSetupAsync("09128000003", "Multi Biz One");
        var setupB = await CreateBookableSetupAsync("09128000004", "Multi Biz Two");
        var customer = await RegisterCustomerAsync("09128100003");

        var bookingA = await CreateUserBookingAsync(
            setupA, customer.AccessToken, DateTime.UtcNow.AddDays(1));
        var bookingB = await CreateUserBookingAsync(
            setupB, customer.AccessToken, DateTime.UtcNow.AddDays(2));

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine", customer.AccessToken);

        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, i => i.Id == bookingA.Id && i.BusinessName == "Multi Biz One");
        Assert.Contains(page.Items, i => i.Id == bookingB.Id && i.BusinessName == "Multi Biz Two");
    }

    [Fact]
    public async Task Guest_bookings_are_not_associated_by_phone_match()
    {
        var setup = await CreateBookableSetupAsync("09128000005", "Phone Match Shop");
        var phone = "09128100005";
        var customer = await RegisterCustomerAsync(phone);

        await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(1), "Guest Same Phone", phone);

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine", customer.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Customer_with_no_customer_record_gets_empty_page()
    {
        var customer = await RegisterCustomerAsync("09128100006");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine", customer.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task Filter_by_status()
    {
        var setup = await CreateBookableSetupAsync("09128000007", "Status Shop");
        var customer = await RegisterCustomerAsync("09128100007");
        var pending = await CreateUserBookingAsync(
            setup, customer.AccessToken, DateTime.UtcNow.AddDays(1));
        var confirmed = await CreateUserBookingAsync(
            setup, customer.AccessToken, DateTime.UtcNow.AddDays(2));
        await SetStatusDirectAsync(confirmed.Id, BookingStatus.Confirmed);

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings/mine?status={(int)BookingStatus.Confirmed}",
            customer.AccessToken);

        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(confirmed.Id, page.Items[0].Id);
        Assert.Equal(BookingStatus.Confirmed, page.Items[0].Status);
        Assert.DoesNotContain(page.Items, i => i.Id == pending.Id);
    }

    [Fact]
    public async Task Filter_by_fromDate()
    {
        var setup = await CreateBookableSetupAsync("09128000008", "FromDate Shop");
        var customer = await RegisterCustomerAsync("09128100008");
        var early = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var late = DateTime.UtcNow.AddDays(5).Date.AddHours(10);
        await CreateUserBookingAsync(setup, customer.AccessToken, early);
        var lateBooking = await CreateUserBookingAsync(setup, customer.AccessToken, late);

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings/mine?fromDate={Uri.EscapeDataString(late.ToString("o"))}",
            customer.AccessToken);

        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(lateBooking.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task Filter_by_toDate()
    {
        var setup = await CreateBookableSetupAsync("09128000009", "ToDate Shop");
        var customer = await RegisterCustomerAsync("09128100009");
        var early = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var late = DateTime.UtcNow.AddDays(5).Date.AddHours(10);
        var earlyBooking = await CreateUserBookingAsync(setup, customer.AccessToken, early);
        await CreateUserBookingAsync(setup, customer.AccessToken, late);

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings/mine?toDate={Uri.EscapeDataString(early.ToString("o"))}",
            customer.AccessToken);

        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(earlyBooking.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task Combined_filters()
    {
        var setup = await CreateBookableSetupAsync("09128000010", "Combined Shop");
        var customer = await RegisterCustomerAsync("09128100010");
        var match = await CreateUserBookingAsync(
            setup, customer.AccessToken, DateTime.UtcNow.AddDays(3).Date.AddHours(12));
        await SetStatusDirectAsync(match.Id, BookingStatus.Confirmed);
        await CreateUserBookingAsync(
            setup, customer.AccessToken, DateTime.UtcNow.AddDays(3).Date.AddHours(14));

        var from = DateTime.UtcNow.AddDays(3).Date;
        var to = DateTime.UtcNow.AddDays(3).Date.AddHours(13);
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings/mine?status={(int)BookingStatus.Confirmed}" +
            $"&fromDate={Uri.EscapeDataString(from.ToString("o"))}" +
            $"&toDate={Uri.EscapeDataString(to.ToString("o"))}",
            customer.AccessToken);

        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(match.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task Default_pagination_values()
    {
        var setup = await CreateBookableSetupAsync("09128000011", "Page Default Shop");
        var customer = await RegisterCustomerAsync("09128100011");
        for (var i = 0; i < 3; i++)
        {
            await CreateUserBookingAsync(
                setup, customer.AccessToken, DateTime.UtcNow.AddDays(i + 1));
        }

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine", customer.AccessToken);

        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.False(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task Custom_page_and_pageSize()
    {
        var setup = await CreateBookableSetupAsync("09128000012", "Page Custom Shop");
        var customer = await RegisterCustomerAsync("09128100012");
        for (var i = 0; i < 5; i++)
        {
            await CreateUserBookingAsync(
                setup, customer.AccessToken, DateTime.UtcNow.AddDays(i + 1));
        }

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine?page=2&pageSize=2", customer.AccessToken);

        var page = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(response);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task PageSize_greater_than_100_is_rejected()
    {
        var customer = await RegisterCustomerAsync("09128100013");
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine?pageSize=101", customer.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_page_is_rejected()
    {
        var customer = await RegisterCustomerAsync("09128100014");
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine?page=0", customer.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FromDate_later_than_toDate_is_rejected()
    {
        var customer = await RegisterCustomerAsync("09128100015");
        var from = DateTime.UtcNow.AddDays(5);
        var to = DateTime.UtcNow.AddDays(1);
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings/mine?fromDate={Uri.EscapeDataString(from.ToString("o"))}" +
            $"&toDate={Uri.EscapeDataString(to.ToString("o"))}",
            customer.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Undefined_status_value_is_rejected()
    {
        var customer = await RegisterCustomerAsync("09128100016");
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine?status=99", customer.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_access_is_rejected()
    {
        var response = await _client.GetAsync("/api/bookings/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Owner_and_employee_cannot_access_other_customers_history()
    {
        var setup = await CreateBookableSetupAsync("09128000017", "Staff Isolation Shop");
        var customer = await RegisterCustomerAsync("09128100017");
        await CreateUserBookingAsync(setup, customer.AccessToken, DateTime.UtcNow.AddDays(1));
        await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(2), "Guest Hist", "09128110017");

        var ownerResponse = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine", setup.OwnerToken);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        var ownerPage = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(ownerResponse);
        Assert.Empty(ownerPage.Items);

        var empLogin = await LoginAsync(setup.EmployeePhone);
        var empResponse = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine", empLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, empResponse.StatusCode);
        var empPage = await ReadAsync<PagedResult<CustomerBookingListItemDto>>(empResponse);
        Assert.Empty(empPage.Items);
    }

    [Fact]
    public async Task Response_does_not_expose_other_customer_or_sensitive_fields()
    {
        var setup = await CreateBookableSetupAsync("09128000018", "Safe Fields Shop");
        var customer = await RegisterCustomerAsync("09128100018");
        await CreateUserBookingAsync(setup, customer.AccessToken, DateTime.UtcNow.AddDays(1));
        await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(2), "Secret Guest", "09128110018");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings/mine", customer.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());

        var item = items[0];
        Assert.True(item.TryGetProperty("businessName", out _));
        Assert.True(item.TryGetProperty("serviceName", out _));
        Assert.True(item.TryGetProperty("employeeName", out _));
        Assert.False(item.TryGetProperty("customerName", out _));
        Assert.False(item.TryGetProperty("customerPhone", out _));
        Assert.False(item.TryGetProperty("customerId", out _));
        Assert.False(item.TryGetProperty("createdByUserId", out _));
        Assert.False(item.TryGetProperty("password", out _));
        Assert.False(item.TryGetProperty("notes", out _));
        Assert.DoesNotContain("Secret Guest", json, StringComparison.Ordinal);
        Assert.DoesNotContain("09128110018", json, StringComparison.Ordinal);
    }

    private async Task<BookingDto> CreateUserBookingAsync(
        SetupContext setup,
        string accessToken,
        DateTime startTime)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/bookings",
            accessToken,
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

    private async Task SetStatusDirectAsync(int bookingId, BookingStatus status)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = await db.Bookings.SingleAsync(b => b.Id == bookingId);
        booking.Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<SetupContext> CreateBookableSetupAsync(string ownerPhone, string businessName)
    {
        var owner = await CreateOwnerWithBusinessAsync(ownerPhone, businessName);
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
            service.Id,
            employeePhone);
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
    private sealed record CustomerBookingListItemDto(
        int Id,
        DateTime StartTime,
        DateTime EndTime,
        BookingStatus Status,
        BookingCreatorType CreatorType,
        string BusinessName,
        int BusinessId,
        string ServiceName,
        int ServiceDurationMinutes,
        string EmployeeName);
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(
        string OwnerToken,
        int BusinessId,
        int EmployeeId,
        int ServiceId,
        string EmployeePhone);
}
