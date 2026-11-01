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
public sealed class BookingConcurrencyTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public BookingConcurrencyTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Non_overlapping_bookings_both_succeed()
    {
        var setup = await CreateBookableSetupAsync("09126000001");
        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(9);

        var first = await CreateGuestAsync(setup, start, "09126200001");
        var second = await CreateGuestAsync(setup, start.AddMinutes(30), "09126200002");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Adjacent_bookings_succeed()
    {
        var setup = await CreateBookableSetupAsync("09126000002");
        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(10);

        var first = await CreateGuestAsync(setup, start, "09126200003");
        var second = await CreateGuestAsync(setup, start.AddMinutes(30), "09126200004");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Overlapping_pending_bookings_are_rejected()
    {
        var setup = await CreateBookableSetupAsync("09126000003");
        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(11);

        var first = await CreateGuestAsync(setup, start, "09126200005");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var overlap = await CreateGuestAsync(setup, start.AddMinutes(15), "09126200006");
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        await AssertCleanConflictBodyAsync(overlap);
    }

    [Fact]
    public async Task Overlapping_confirmed_bookings_are_rejected()
    {
        var setup = await CreateBookableSetupAsync("09126000004");
        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(12);

        await SeedBookingAsync(setup, start, BookingStatus.Confirmed);

        var overlap = await CreateGuestAsync(setup, start.AddMinutes(10), "09126200007");
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        await AssertCleanConflictBodyAsync(overlap);
    }

    [Fact]
    public async Task Cancelled_bookings_do_not_block_new_bookings()
    {
        var setup = await CreateBookableSetupAsync("09126000005");
        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(13);

        await SeedBookingAsync(setup, start, BookingStatus.Cancelled);

        var response = await CreateGuestAsync(setup, start, "09126200008");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Expired_bookings_do_not_block_new_bookings()
    {
        var setup = await CreateBookableSetupAsync("09126000006");
        var start = DateTime.UtcNow.AddDays(1).Date.AddHours(14);

        await SeedBookingAsync(setup, start, BookingStatus.Expired);

        var response = await CreateGuestAsync(setup, start, "09126200009");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_overlapping_requests_only_one_succeeds()
    {
        var setup = await CreateBookableSetupAsync("09126000007");
        var start = DateTime.UtcNow.AddDays(2).Date.AddHours(9);

        var task1 = CreateGuestAsync(setup, start, "09126200010");
        var task2 = CreateGuestAsync(setup, start, "09126200011");
        var results = await Task.WhenAll(task1, task2);

        var created = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, created);
        Assert.Equal(1, conflicts);

        var conflict = results.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        await AssertCleanConflictBodyAsync(conflict);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Bookings.CountAsync(b =>
            b.EmployeeId == setup.EmployeeId &&
            b.StartTime == start);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Concurrent_requests_for_different_employees_both_succeed()
    {
        var setup = await CreateBookableSetupAsync("09126000008");
        var secondEmployee = await CreateEmployeeAsync(
            setup.OwnerToken,
            setup.BusinessId,
            "09126100008");
        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{secondEmployee.Id}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId });
        assign.EnsureSuccessStatusCode();

        var start = DateTime.UtcNow.AddDays(2).Date.AddHours(10);
        var setupB = setup with { EmployeeId = secondEmployee.Id };

        var results = await Task.WhenAll(
            CreateGuestAsync(setup, start, "09126200012"),
            CreateGuestAsync(setupB, start, "09126200013"));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
    }

    [Fact]
    public async Task Conflict_response_does_not_expose_database_internals()
    {
        var setup = await CreateBookableSetupAsync("09126000009");
        var start = DateTime.UtcNow.AddDays(2).Date.AddHours(11);

        Assert.Equal(HttpStatusCode.Created, (await CreateGuestAsync(setup, start, "09126200014")).StatusCode);

        var conflict = await CreateGuestAsync(setup, start, "09126200015");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertCleanConflictBodyAsync(conflict);
    }

    [Fact]
    public async Task Failed_booking_does_not_leave_orphaned_guest_customer()
    {
        var setup = await CreateBookableSetupAsync("09126000010");
        var start = DateTime.UtcNow.AddDays(2).Date.AddHours(12);
        var loserPhone = "09126200017";

        Assert.Equal(HttpStatusCode.Created, (await CreateGuestAsync(setup, start, "09126200016")).StatusCode);

        var conflict = await CreateGuestAsync(setup, start, loserPhone);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await db.Customers.AnyAsync(c =>
            c.BusinessId == setup.BusinessId &&
            c.PhoneNumber == loserPhone));

        Assert.Equal(1, await db.Bookings.CountAsync(b =>
            b.EmployeeId == setup.EmployeeId &&
            b.StartTime == start));
    }

    private async Task<HttpResponseMessage> CreateGuestAsync(
        SetupContext setup,
        DateTime startTime,
        string phoneNumber) =>
        await _client.PostAsJsonAsync("/api/bookings/guest", new
        {
            businessId = setup.BusinessId,
            serviceId = setup.ServiceId,
            employeeId = setup.EmployeeId,
            startTime,
            endTime = startTime.AddMinutes(30),
            name = $"Guest {phoneNumber}",
            phoneNumber
        });

    private async Task SeedBookingAsync(
        SetupContext setup,
        DateTime startTime,
        BookingStatus status)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = new Customer
        {
            BusinessId = setup.BusinessId,
            Name = $"Seed {status}",
            PhoneNumber = $"091269{((int)status):D5}",
            UserId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.Bookings.Add(new Booking
        {
            BusinessId = setup.BusinessId,
            CustomerId = customer.Id,
            ServiceId = setup.ServiceId,
            EmployeeId = setup.EmployeeId,
            StartTime = startTime,
            EndTime = startTime.AddMinutes(30),
            Status = status,
            CreatorType = BookingCreatorType.Guest,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task AssertCleanConflictBodyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("رزرو", body, StringComparison.Ordinal);
        Assert.DoesNotContain("exclusion", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("23P01", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Postgres", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EX_Bookings", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tstzrange", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SetupContext> CreateBookableSetupAsync(string ownerPhone)
    {
        var owner = await CreateOwnerWithBusinessAsync(ownerPhone, $"Shop {ownerPhone}");
        var employeePhone = ownerPhone.Replace("09126", "09127", StringComparison.Ordinal);
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
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(string OwnerToken, int BusinessId, int EmployeeId, int ServiceId);
}
