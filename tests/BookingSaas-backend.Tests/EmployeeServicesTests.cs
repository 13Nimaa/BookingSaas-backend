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
public sealed class EmployeeServicesTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public EmployeeServicesTests(AuthFixture fixture) => _fixture = fixture;

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
    public async Task Assign_service_to_employee_succeeds()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000001");

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var service = await ReadAsync<ServiceDto>(response);
        Assert.Equal(setup.ServiceId, service.Id);
        Assert.Equal(setup.BusinessId, service.BusinessId);

        var list = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var services = await ReadAsync<List<ServiceDto>>(list);
        Assert.Contains(services, s => s.Id == setup.ServiceId);
    }

    [Fact]
    public async Task Assign_duplicate_service_returns_conflict()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000002");

        var first = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Assign_service_from_another_business_returns_conflict()
    {
        var ownerA = await CreateOwnerWithEmployeeAndServiceAsync("09122000003");
        var ownerB = await CreateOwnerWithBusinessAsync("09122000004", "Other Shop");
        var otherService = await CreateServiceAsync(ownerB.AccessToken, ownerB.BusinessId, "Other Cut");

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{ownerA.EmployeeId}/services",
            ownerA.OwnerToken,
            new { serviceId = otherService.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Other_owner_cannot_manage_employee_services()
    {
        var ownerA = await CreateOwnerWithEmployeeAndServiceAsync("09122000005");
        var ownerB = await CreateOwnerWithBusinessAsync("09122000006", "Rival Shop");

        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{ownerA.EmployeeId}/services",
            ownerB.AccessToken,
            new { serviceId = ownerA.ServiceId });

        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);

        var list = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/employees/{ownerA.EmployeeId}/services",
            ownerB.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Remove_assigned_service_succeeds()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000007");

        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);

        var remove = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        var list = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken);
        var services = await ReadAsync<List<ServiceDto>>(list);
        Assert.DoesNotContain(services, s => s.Id == setup.ServiceId);
    }

    [Fact]
    public async Task Remove_nonexistent_assignment_returns_not_found()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000008");

        var remove = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken);

        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
    }

    [Fact]
    public async Task Assign_service_persists_null_overrides()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000009");

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var assignment = await GetAssignmentAsync(setup.EmployeeId, setup.ServiceId);
        Assert.Null(assignment.PriceOverride);
        Assert.Null(assignment.DurationMinutesOverride);
    }

    [Fact]
    public async Task EmployeeService_can_store_custom_price_override()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000010");
        await AssignViaApiAsync(setup);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var assignment = await db.EmployeeServices.SingleAsync(es =>
                es.EmployeesId == setup.EmployeeId &&
                es.ServicesId == setup.ServiceId);
            assignment.PriceOverride = 150000m;
            await db.SaveChangesAsync();
        }

        var persisted = await GetAssignmentAsync(setup.EmployeeId, setup.ServiceId);
        Assert.Equal(150000m, persisted.PriceOverride);
        Assert.Null(persisted.DurationMinutesOverride);
    }

    [Fact]
    public async Task EmployeeService_can_store_custom_duration_override()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000011");
        await AssignViaApiAsync(setup);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var assignment = await db.EmployeeServices.SingleAsync(es =>
                es.EmployeesId == setup.EmployeeId &&
                es.ServicesId == setup.ServiceId);
            assignment.DurationMinutesOverride = 45;
            await db.SaveChangesAsync();
        }

        var persisted = await GetAssignmentAsync(setup.EmployeeId, setup.ServiceId);
        Assert.Null(persisted.PriceOverride);
        Assert.Equal(45, persisted.DurationMinutesOverride);
    }

    [Fact]
    public async Task EmployeeService_overrides_can_be_null_independently()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000012");
        await AssignViaApiAsync(setup);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var assignment = await db.EmployeeServices.SingleAsync(es =>
                es.EmployeesId == setup.EmployeeId &&
                es.ServicesId == setup.ServiceId);
            assignment.PriceOverride = 200000m;
            assignment.DurationMinutesOverride = 60;
            await db.SaveChangesAsync();
        }

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var assignment = await db.EmployeeServices.SingleAsync(es =>
                es.EmployeesId == setup.EmployeeId &&
                es.ServicesId == setup.ServiceId);
            assignment.PriceOverride = null;
            await db.SaveChangesAsync();
        }

        var afterPriceCleared = await GetAssignmentAsync(setup.EmployeeId, setup.ServiceId);
        Assert.Null(afterPriceCleared.PriceOverride);
        Assert.Equal(60, afterPriceCleared.DurationMinutesOverride);

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var assignment = await db.EmployeeServices.SingleAsync(es =>
                es.EmployeesId == setup.EmployeeId &&
                es.ServicesId == setup.ServiceId);
            assignment.DurationMinutesOverride = null;
            assignment.PriceOverride = 175000m;
            await db.SaveChangesAsync();
        }

        var afterDurationCleared = await GetAssignmentAsync(setup.EmployeeId, setup.ServiceId);
        Assert.Equal(175000m, afterDurationCleared.PriceOverride);
        Assert.Null(afterDurationCleared.DurationMinutesOverride);
    }

    [Fact]
    public async Task Duplicate_EmployeeService_assignment_is_prevented_at_database()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000013");
        await AssignViaApiAsync(setup);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EmployeeServices.Add(new EmployeeService
        {
            EmployeesId = setup.EmployeeId,
            ServicesId = setup.ServiceId
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private async Task AssignViaApiAsync(SetupContext setup)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<EmployeeService> GetAssignmentAsync(int employeeId, int serviceId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.EmployeeServices
            .AsNoTracking()
            .SingleAsync(es =>
                es.EmployeesId == employeeId &&
                es.ServicesId == serviceId);
    }

    private async Task<SetupContext> CreateOwnerWithEmployeeAndServiceAsync(string ownerPhone)
    {
        var owner = await CreateOwnerWithBusinessAsync(ownerPhone, $"Shop {ownerPhone}");
        var employeePhone = ownerPhone.Replace("09122", "09123", StringComparison.Ordinal);
        var employee = await CreateEmployeeAsync(owner.AccessToken, owner.BusinessId, employeePhone);
        var service = await CreateServiceAsync(owner.AccessToken, owner.BusinessId, "Haircut");

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
