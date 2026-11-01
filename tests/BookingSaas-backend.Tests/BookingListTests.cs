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
public sealed class BookingListTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public BookingListTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Owner_lists_bookings_for_own_business()
    {
        var setup = await CreateBookableSetupAsync("09125000001");
        var booking = await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(1), "Ali Reza", "09125100001");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings", setup.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(booking.Id, page.Items[0].Id);
        Assert.Equal(setup.BusinessId, page.Items[0].BusinessId);
        Assert.Equal("Ali Reza", page.Items[0].CustomerName);
        Assert.Equal("09125100001", page.Items[0].CustomerPhone);
        Assert.Equal("Haircut", page.Items[0].ServiceName);
        Assert.Equal(30, page.Items[0].ServiceDurationMinutes);
        Assert.False(string.IsNullOrWhiteSpace(page.Items[0].EmployeeName));
    }

    [Fact]
    public async Task Owner_cannot_see_another_business_bookings()
    {
        var setupA = await CreateBookableSetupAsync("09125000002");
        var setupB = await CreateBookableSetupAsync("09125000003");
        await CreateGuestBookingAsync(
            setupA, DateTime.UtcNow.AddDays(1), "Only A", "09125100002");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings", setupB.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Employee_sees_only_own_bookings()
    {
        var setup = await CreateTwoEmployeeSetupAsync("09125000004");
        await CreateGuestBookingAsync(
            setup.Primary, DateTime.UtcNow.AddDays(1), "For Emp1", "09125100004");
        await CreateGuestBookingAsync(
            setup.Secondary, DateTime.UtcNow.AddDays(2), "For Emp2", "09125110004");

        var empLogin = await LoginAsync(setup.PrimaryEmployeePhone);
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings", empLogin.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal("For Emp1", page.Items[0].CustomerName);
        Assert.Equal(
            setup.Primary.EmployeeId,
            await GetBookingEmployeeIdAsync(page.Items[0].Id));
    }

    [Fact]
    public async Task Employee_cannot_access_other_employee_via_employeeId_filter()
    {
        var setup = await CreateTwoEmployeeSetupAsync("09125000005");
        await CreateGuestBookingAsync(
            setup.Primary, DateTime.UtcNow.AddDays(1), "Emp1 Booking", "09125100005");
        await CreateGuestBookingAsync(
            setup.Secondary, DateTime.UtcNow.AddDays(2), "Emp2 Booking", "09125110005");

        var empLogin = await LoginAsync(setup.PrimaryEmployeePhone);
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings?employeeId={setup.Secondary.EmployeeId}",
            empLogin.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal("Emp1 Booking", page.Items[0].CustomerName);
        Assert.DoesNotContain(page.Items, i => i.CustomerName == "Emp2 Booking");
    }

    [Fact]
    public async Task Anonymous_access_is_rejected()
    {
        var response = await _client.GetAsync("/api/bookings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_access_is_rejected()
    {
        var customer = await RegisterCustomerAsync("09125000006");
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings", customer.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Filter_by_status()
    {
        var setup = await CreateBookableSetupAsync("09125000007");
        var pending = await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(1), "Pending", "09125100007");
        var confirmed = await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(2), "Confirmed", "09125110007");
        await SetStatusDirectAsync(confirmed.Id, BookingStatus.Confirmed);

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings?status={(int)BookingStatus.Confirmed}",
            setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(confirmed.Id, page.Items[0].Id);
        Assert.Equal(BookingStatus.Confirmed, page.Items[0].Status);
        Assert.DoesNotContain(page.Items, i => i.Id == pending.Id);
    }

    [Fact]
    public async Task Filter_by_employeeId()
    {
        var setup = await CreateTwoEmployeeSetupAsync("09125000008");
        await CreateGuestBookingAsync(
            setup.Primary, DateTime.UtcNow.AddDays(1), "E1", "09125100008");
        await CreateGuestBookingAsync(
            setup.Secondary, DateTime.UtcNow.AddDays(2), "E2", "09125110008");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings?employeeId={setup.Secondary.EmployeeId}",
            setup.Primary.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal("E2", page.Items[0].CustomerName);
    }

    [Fact]
    public async Task Filter_by_customerId()
    {
        var setup = await CreateBookableSetupAsync("09125000009");
        var a = await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(1), "Cust A", "09125100009");
        await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(2), "Cust B", "09125110009");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings?customerId={a.CustomerId}",
            setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(a.Id, page.Items[0].Id);
        Assert.Equal("Cust A", page.Items[0].CustomerName);
    }

    [Fact]
    public async Task Filter_by_serviceId()
    {
        var setup = await CreateBookableSetupAsync("09125000010");
        var otherService = await CreateServiceAsync(
            setup.OwnerToken, setup.BusinessId, "Coloring");
        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = otherService.Id });
        assign.EnsureSuccessStatusCode();

        await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(1), "Hair", "09125100010");
        await CreateGuestBookingAsync(
            setup with { ServiceId = otherService.Id },
            DateTime.UtcNow.AddDays(2),
            "Color",
            "09125110010");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings?serviceId={otherService.Id}",
            setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal("Color", page.Items[0].CustomerName);
        Assert.Equal("Coloring", page.Items[0].ServiceName);
    }

    [Fact]
    public async Task Filter_by_fromDate()
    {
        var setup = await CreateBookableSetupAsync("09125000011");
        var early = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var late = DateTime.UtcNow.AddDays(5).Date.AddHours(10);
        await CreateGuestBookingAsync(setup, early, "Early", "09125100011");
        await CreateGuestBookingAsync(setup, late, "Late", "09125110011");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings?fromDate={Uri.EscapeDataString(late.ToString("o"))}",
            setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal("Late", page.Items[0].CustomerName);
    }

    [Fact]
    public async Task Filter_by_toDate()
    {
        var setup = await CreateBookableSetupAsync("09125000012");
        var early = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        var late = DateTime.UtcNow.AddDays(5).Date.AddHours(10);
        await CreateGuestBookingAsync(setup, early, "Early", "09125100012");
        await CreateGuestBookingAsync(setup, late, "Late", "09125110012");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings?toDate={Uri.EscapeDataString(early.ToString("o"))}",
            setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal("Early", page.Items[0].CustomerName);
    }

    [Fact]
    public async Task Search_by_customer_name()
    {
        var setup = await CreateBookableSetupAsync("09125000013");
        await CreateGuestBookingAsync(setup, DateTime.UtcNow.AddDays(1), "Sara Ahmadi", "09125100013");
        await CreateGuestBookingAsync(setup, DateTime.UtcNow.AddDays(2), "Reza Karimi", "09125110013");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings?search=sara", setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal("Sara Ahmadi", page.Items[0].CustomerName);
    }

    [Fact]
    public async Task Search_by_customer_phone()
    {
        var setup = await CreateBookableSetupAsync("09125000014");
        await CreateGuestBookingAsync(setup, DateTime.UtcNow.AddDays(1), "Phone One", "09125100014");
        await CreateGuestBookingAsync(setup, DateTime.UtcNow.AddDays(2), "Phone Two", "09125110014");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings?search=09125110014", setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal("Phone Two", page.Items[0].CustomerName);
    }

    [Fact]
    public async Task Combined_filters()
    {
        var setup = await CreateBookableSetupAsync("09125000015");
        var match = await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(3).Date.AddHours(12), "Match Me", "09125100015");
        await SetStatusDirectAsync(match.Id, BookingStatus.Confirmed);
        await CreateGuestBookingAsync(
            setup, DateTime.UtcNow.AddDays(3).Date.AddHours(14), "Other", "09125110015");

        var from = DateTime.UtcNow.AddDays(3).Date;
        var to = DateTime.UtcNow.AddDays(3).Date.AddHours(23);
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings?status={(int)BookingStatus.Confirmed}" +
            $"&serviceId={setup.ServiceId}" +
            $"&employeeId={setup.EmployeeId}" +
            $"&customerId={match.CustomerId}" +
            $"&fromDate={Uri.EscapeDataString(from.ToString("o"))}" +
            $"&toDate={Uri.EscapeDataString(to.ToString("o"))}" +
            "&search=match",
            setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Single(page.Items);
        Assert.Equal(match.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task Default_pagination_values()
    {
        var setup = await CreateBookableSetupAsync("09125000016");
        for (var i = 0; i < 3; i++)
        {
            await CreateGuestBookingAsync(
                setup,
                DateTime.UtcNow.AddDays(i + 1),
                $"Cust {i}",
                $"091251{i:D5}");
        }

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings", setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
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
        var setup = await CreateBookableSetupAsync("09125000017");
        for (var i = 0; i < 5; i++)
        {
            await CreateGuestBookingAsync(
                setup,
                DateTime.UtcNow.AddDays(i + 1),
                $"Page Cust {i}",
                $"091252{i:D5}");
        }

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings?page=2&pageSize=2", setup.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
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
        var setup = await CreateBookableSetupAsync("09125000018");
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings?pageSize=101", setup.OwnerToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_page_is_rejected()
    {
        var setup = await CreateBookableSetupAsync("09125000019");
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings?page=0", setup.OwnerToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FromDate_later_than_toDate_is_rejected()
    {
        var setup = await CreateBookableSetupAsync("09125000020");
        var from = DateTime.UtcNow.AddDays(5);
        var to = DateTime.UtcNow.AddDays(1);
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/bookings?fromDate={Uri.EscapeDataString(from.ToString("o"))}" +
            $"&toDate={Uri.EscapeDataString(to.ToString("o"))}",
            setup.OwnerToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Empty_result_returns_valid_pagination_metadata()
    {
        var setup = await CreateBookableSetupAsync("09125000021");
        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings", setup.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.False(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task Stable_sort_when_startTimes_match()
    {
        var setup = await CreateTwoEmployeeSetupAsync("09125000022");
        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(11);
        var first = await CreateGuestBookingAsync(
            setup.Primary, start, "First", "09125100022");
        var second = await CreateGuestBookingAsync(
            setup.Secondary, start, "Second", "09125110022");

        var response = await SendAuthorizedAsync(
            HttpMethod.Get, "/api/bookings", setup.Primary.OwnerToken);

        var page = await ReadAsync<PagedResult<BookingListItemDto>>(response);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(second.Id, page.Items[0].Id);
        Assert.Equal(first.Id, page.Items[1].Id);
        Assert.True(page.Items[0].Id > page.Items[1].Id);
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

    private async Task<int> GetBookingEmployeeIdAsync(int bookingId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings
            .Where(b => b.Id == bookingId)
            .Select(b => b.EmployeeId)
            .SingleAsync();
    }

    private async Task<SetupContext> CreateBookableSetupAsync(string ownerPhone)
    {
        var owner = await CreateOwnerWithBusinessAsync(ownerPhone, $"Shop {ownerPhone}");
        var employeePhone = ownerPhone.Replace("09125", "09126", StringComparison.Ordinal);
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
        var secondPhone = ownerPhone.Replace("09125", "09127", StringComparison.Ordinal);
        var secondEmployee = await CreateEmployeeAsync(
            primary.OwnerToken, primary.BusinessId, secondPhone);

        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{secondEmployee.Id}/services",
            primary.OwnerToken,
            new { serviceId = primary.ServiceId });
        assign.EnsureSuccessStatusCode();

        var secondary = primary with { EmployeeId = secondEmployee.Id };
        var primaryEmployeePhone = ownerPhone.Replace("09125", "09126", StringComparison.Ordinal);
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
    private sealed record BookingListItemDto(
        int Id,
        DateTime StartTime,
        DateTime EndTime,
        BookingStatus Status,
        BookingCreatorType CreatorType,
        string CustomerName,
        string? CustomerPhone,
        string ServiceName,
        int ServiceDurationMinutes,
        string EmployeeName,
        int BusinessId);
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(string OwnerToken, int BusinessId, int EmployeeId, int ServiceId);
    private sealed record TwoEmployeeSetup(
        SetupContext Primary,
        SetupContext Secondary,
        string PrimaryEmployeePhone,
        string SecondaryEmployeePhone);
}
