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
public sealed class EmployeeServicesTests : IAsyncLifetime
{
    private const decimal DefaultPrice = 100000m;
    private const int DefaultDurationMinutes = 30;

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
    public async Task Get_returns_defaults_and_effective_values_when_no_overrides()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000001");
        await AssignViaApiAsync(setup);

        var list = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var services = await ReadAsync<List<EmployeeServiceDto>>(list);
        var service = Assert.Single(services);
        Assert.Equal(setup.ServiceId, service.Id);
        Assert.Equal(DefaultPrice, service.DefaultPrice);
        Assert.Equal(DefaultDurationMinutes, service.DefaultDurationMinutes);
        Assert.Null(service.PriceOverride);
        Assert.Null(service.DurationMinutesOverride);
        Assert.Equal(DefaultPrice, service.EffectivePrice);
        Assert.Equal(DefaultDurationMinutes, service.EffectiveDurationMinutes);
        Assert.Equal(setup.BusinessId, service.BusinessId);
    }

    [Fact]
    public async Task Get_returns_custom_overrides_and_effective_values()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000002");
        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new
            {
                serviceId = setup.ServiceId,
                priceOverride = 300000m,
                durationMinutesOverride = 45
            });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);

        var list = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken);
        var service = Assert.Single(await ReadAsync<List<EmployeeServiceDto>>(list));

        Assert.Equal(DefaultPrice, service.DefaultPrice);
        Assert.Equal(DefaultDurationMinutes, service.DefaultDurationMinutes);
        Assert.Equal(300000m, service.PriceOverride);
        Assert.Equal(45, service.DurationMinutesOverride);
        Assert.Equal(300000m, service.EffectivePrice);
        Assert.Equal(45, service.EffectiveDurationMinutes);
    }

    [Fact]
    public async Task Get_calculates_effective_values_when_only_one_override_is_set()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000003");
        var assign = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId, priceOverride = 250000m });
        Assert.Equal(HttpStatusCode.Created, assign.StatusCode);

        var list = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken);
        var service = Assert.Single(await ReadAsync<List<EmployeeServiceDto>>(list));

        Assert.Equal(250000m, service.PriceOverride);
        Assert.Null(service.DurationMinutesOverride);
        Assert.Equal(250000m, service.EffectivePrice);
        Assert.Equal(DefaultDurationMinutes, service.EffectiveDurationMinutes);
    }

    [Fact]
    public async Task Post_creates_assignment_with_both_overrides_null()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000004");

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new
            {
                serviceId = setup.ServiceId,
                priceOverride = (decimal?)null,
                durationMinutesOverride = (int?)null
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<EmployeeServiceDto>(response);
        Assert.Null(created.PriceOverride);
        Assert.Null(created.DurationMinutesOverride);
        Assert.Equal(DefaultPrice, created.EffectivePrice);
        Assert.Equal(DefaultDurationMinutes, created.EffectiveDurationMinutes);

        var persisted = await GetAssignmentAsync(setup.EmployeeId, setup.ServiceId);
        Assert.Null(persisted.PriceOverride);
        Assert.Null(persisted.DurationMinutesOverride);
    }

    [Fact]
    public async Task Post_creates_assignment_with_custom_price_and_duration()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000005");

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new
            {
                serviceId = setup.ServiceId,
                priceOverride = 180000m,
                durationMinutesOverride = 50
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<EmployeeServiceDto>(response);
        Assert.Equal(180000m, created.PriceOverride);
        Assert.Equal(50, created.DurationMinutesOverride);
        Assert.Equal(180000m, created.EffectivePrice);
        Assert.Equal(50, created.EffectiveDurationMinutes);
        Assert.Equal(DefaultPrice, created.DefaultPrice);
        Assert.Equal(DefaultDurationMinutes, created.DefaultDurationMinutes);
    }

    [Fact]
    public async Task Post_supports_serviceId_only_request()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000006");

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<EmployeeServiceDto>(response);
        Assert.Equal(setup.ServiceId, created.Id);
        Assert.Null(created.PriceOverride);
        Assert.Null(created.DurationMinutesOverride);
    }

    [Fact]
    public async Task Post_rejects_invalid_price_or_duration()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000007");

        var invalidPrice = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId, priceOverride = -1m });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPrice.StatusCode);

        var invalidDuration = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new { serviceId = setup.ServiceId, durationMinutesOverride = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalidDuration.StatusCode);
    }

    [Fact]
    public async Task Assign_duplicate_service_returns_conflict()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000008");
        await AssignViaApiAsync(setup);

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
        var ownerA = await CreateOwnerWithEmployeeAndServiceAsync("09122000009");
        var ownerB = await CreateOwnerWithBusinessAsync("09122000010", "Other Shop");
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
        var ownerA = await CreateOwnerWithEmployeeAndServiceAsync("09122000011");
        var ownerB = await CreateOwnerWithBusinessAsync("09122000012", "Rival Shop");

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
    public async Task Put_updates_both_overrides()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000013");
        await AssignViaApiAsync(setup);

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken,
            new { priceOverride = 220000m, durationMinutesOverride = 40 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<EmployeeServiceDto>(response);
        Assert.Equal(220000m, updated.PriceOverride);
        Assert.Equal(40, updated.DurationMinutesOverride);
        Assert.Equal(220000m, updated.EffectivePrice);
        Assert.Equal(40, updated.EffectiveDurationMinutes);
    }

    [Fact]
    public async Task Put_updates_only_price_override()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000014");
        await AssignWithOverridesAsync(setup, 150000m, 55);

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken,
            new { priceOverride = 175000m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<EmployeeServiceDto>(response);
        Assert.Equal(175000m, updated.PriceOverride);
        Assert.Equal(55, updated.DurationMinutesOverride);
    }

    [Fact]
    public async Task Put_updates_only_duration_override()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000015");
        await AssignWithOverridesAsync(setup, 150000m, 55);

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken,
            new { durationMinutesOverride = 70 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<EmployeeServiceDto>(response);
        Assert.Equal(150000m, updated.PriceOverride);
        Assert.Equal(70, updated.DurationMinutesOverride);
    }

    [Fact]
    public async Task Put_resets_price_override_to_null()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000016");
        await AssignWithOverridesAsync(setup, 150000m, 55);

        var response = await SendAuthorizedRawAsync(
            HttpMethod.Put,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken,
            """{"priceOverride":null}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<EmployeeServiceDto>(response);
        Assert.Null(updated.PriceOverride);
        Assert.Equal(55, updated.DurationMinutesOverride);
        Assert.Equal(DefaultPrice, updated.EffectivePrice);
        Assert.Equal(55, updated.EffectiveDurationMinutes);
    }

    [Fact]
    public async Task Put_resets_duration_override_to_null()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000017");
        await AssignWithOverridesAsync(setup, 150000m, 55);

        var response = await SendAuthorizedRawAsync(
            HttpMethod.Put,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken,
            """{"durationMinutesOverride":null}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<EmployeeServiceDto>(response);
        Assert.Equal(150000m, updated.PriceOverride);
        Assert.Null(updated.DurationMinutesOverride);
        Assert.Equal(DefaultDurationMinutes, updated.EffectiveDurationMinutes);
    }

    [Fact]
    public async Task Put_resets_both_overrides_to_null()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000018");
        await AssignWithOverridesAsync(setup, 150000m, 55);

        var response = await SendAuthorizedRawAsync(
            HttpMethod.Put,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken,
            """{"priceOverride":null,"durationMinutesOverride":null}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<EmployeeServiceDto>(response);
        Assert.Null(updated.PriceOverride);
        Assert.Null(updated.DurationMinutesOverride);
        Assert.Equal(DefaultPrice, updated.EffectivePrice);
        Assert.Equal(DefaultDurationMinutes, updated.EffectiveDurationMinutes);
    }

    [Fact]
    public async Task Put_rejects_invalid_values()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000019");
        await AssignViaApiAsync(setup);

        var invalidPrice = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken,
            new { priceOverride = -5m });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPrice.StatusCode);

        var invalidDuration = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken,
            new { durationMinutesOverride = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, invalidDuration.StatusCode);
    }

    [Fact]
    public async Task Put_returns_not_found_for_nonexistent_assignment()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000020");

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken,
            new { priceOverride = 120000m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_prevents_cross_business_updates()
    {
        var ownerA = await CreateOwnerWithEmployeeAndServiceAsync("09122000021");
        await AssignViaApiAsync(ownerA);
        var ownerB = await CreateOwnerWithBusinessAsync("09122000022", "Rival Shop");

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/employees/{ownerA.EmployeeId}/services/{ownerA.ServiceId}",
            ownerB.AccessToken,
            new { priceOverride = 999999m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var persisted = await GetAssignmentAsync(ownerA.EmployeeId, ownerA.ServiceId);
        Assert.Null(persisted.PriceOverride);
    }

    [Fact]
    public async Task Remove_assigned_service_succeeds()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000023");
        await AssignViaApiAsync(setup);

        var remove = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken);
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);

        var list = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken);
        var services = await ReadAsync<List<EmployeeServiceDto>>(list);
        Assert.DoesNotContain(services, s => s.Id == setup.ServiceId);
    }

    [Fact]
    public async Task Remove_nonexistent_assignment_returns_not_found()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000024");

        var remove = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/employees/{setup.EmployeeId}/services/{setup.ServiceId}",
            setup.OwnerToken);

        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
    }

    [Fact]
    public async Task Duplicate_EmployeeService_assignment_is_prevented_at_database()
    {
        var setup = await CreateOwnerWithEmployeeAndServiceAsync("09122000025");
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

    private async Task AssignWithOverridesAsync(
        SetupContext setup,
        decimal priceOverride,
        int durationMinutesOverride)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/api/employees/{setup.EmployeeId}/services",
            setup.OwnerToken,
            new
            {
                serviceId = setup.ServiceId,
                priceOverride,
                durationMinutesOverride
            });
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
    private sealed record OwnerContext(string AccessToken, int BusinessId);
    private sealed record SetupContext(string OwnerToken, int BusinessId, int EmployeeId, int ServiceId);
}
