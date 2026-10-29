using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingSaas_backend.Tests;

[Collection(AuthCollection.Name)]
public sealed class BussinessTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public BussinessTests(AuthFixture fixture) => _fixture = fixture;

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

    // --- Business creation (owner registration) ---

    [Fact]
    public async Task RegisterBusiness_as_authenticated_customer_creates_business_and_elevates_to_owner()
    {
        var customer = await RegisterCustomerAsync("09121000001");

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/Business/register",
            customer.AccessToken,
            new
            {
                name = "Nima Salon",
                city = "Tehran",
                address = "Valiasr St 1",
                description = "Hair salon"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await ReadAsync<AuthResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));

        var session = await GetSessionAsync(auth.AccessToken);
        Assert.Contains("BusinessOwner", session.Roles);

        var mine = await GetMineAsync(auth.AccessToken);
        Assert.True(mine.HasBusiness);
        Assert.NotNull(mine.Business);
        Assert.Equal("Nima Salon", mine.Business!.Name);
        Assert.Equal(customer.User.UserId, mine.Business.OwnerId);
    }

    [Fact]
    public async Task RegisterBusiness_without_authentication_returns_unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/Business/register", ValidBusinessPayload());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_create_business_via_admin_only_endpoint()
    {
        var customer = await RegisterCustomerAsync("09121000002");

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/Business",
            customer.AccessToken,
            ValidBusinessPayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterBusiness_with_null_required_fields_is_rejected()
    {
        var customer = await RegisterCustomerAsync("09121000003");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Business/register")
        {
            Content = new StringContent(
                """{"name":null,"city":null,"address":null,"description":null}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customer.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterBusiness_twice_for_same_owner_returns_conflict()
    {
        var customer = await RegisterCustomerAsync("09121000004");
        var first = await RegisterBusinessAsync(customer.AccessToken, "First Shop");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var ownerToken = (await ReadAsync<AuthResponse>(first)).AccessToken;
        var second = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/Business/register",
            ownerToken,
            new
            {
                name = "Second Shop",
                city = "Shiraz",
                address = "Another St",
                description = (string?)null
            });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_business_via_admin_endpoint_and_owns_it()
    {
        var admin = await CreateAdminAsync("09121000005");

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/Business",
            admin.AccessToken,
            ValidBusinessPayload("Admin Biz", "Isfahan"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<BusinessDto>(response);
        Assert.Equal("Admin Biz", created.Name);
        Assert.Equal(admin.UserId, created.OwnerId);
        Assert.True(created.Id > 0);
    }

    // --- Business retrieval ---

    [Fact]
    public async Task Owner_can_retrieve_own_business_via_mine()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000006", "Mine Shop");

        var mine = await GetMineAsync(owner.AccessToken);

        Assert.True(mine.HasBusiness);
        Assert.Equal("Mine Shop", mine.Business!.Name);
        Assert.Equal(owner.UserId, mine.Business.OwnerId);
    }

    [Fact]
    public async Task Owner_mine_endpoint_does_not_return_another_owners_business()
    {
        var ownerA = await CreateOwnerWithBusinessAsync("09121000007", "Owner A Shop");
        var ownerB = await CreateOwnerWithBusinessAsync("09121000008", "Owner B Shop");

        var mineB = await GetMineAsync(ownerB.AccessToken);

        Assert.True(mineB.HasBusiness);
        Assert.Equal("Owner B Shop", mineB.Business!.Name);
        Assert.NotEqual(ownerA.UserId, mineB.Business.OwnerId);
        Assert.Equal(ownerB.UserId, mineB.Business.OwnerId);
    }

    [Fact]
    public async Task Public_list_and_detail_are_accessible_without_authentication()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000009", "Public Shop");

        var listResponse = await _client.GetAsync("/api/Business");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await ReadAsync<List<BusinessDto>>(listResponse);
        Assert.Contains(list, b => b.Name == "Public Shop" && b.OwnerId == owner.UserId);

        var detailResponse = await _client.GetAsync($"/api/Business/{owner.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await ReadAsync<BusinessDto>(detailResponse);
        Assert.Equal(owner.BusinessId, detail.Id);
        Assert.Equal("Public Shop", detail.Name);
    }

    [Fact]
    public async Task GetById_with_nonexistent_id_returns_not_found()
    {
        var response = await _client.GetAsync("/api/Business/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_without_authentication_returns_unauthorized()
    {
        var response = await _client.GetAsync("/api/Business/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Business update ---

    [Fact]
    public async Task Owner_can_update_own_business_fields()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000010", "Before Update");

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/Business/{owner.BusinessId}",
            owner.AccessToken,
            new
            {
                name = "After Update",
                city = "Mashhad",
                address = "Updated Address",
                description = "Updated desc"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<UpdateBusinessResponse>(response);
        Assert.Equal("Business updated successfully", body.Message);
        Assert.Equal("After Update", body.Data.Name);
        Assert.Equal("Mashhad", body.Data.City);
        Assert.Equal(owner.UserId, body.Data.OwnerId);

        var detail = await ReadAsync<BusinessDto>(
            await _client.GetAsync($"/api/Business/{owner.BusinessId}"));
        Assert.Equal("After Update", detail.Name);
        Assert.Equal(owner.UserId, detail.OwnerId);
    }

    [Fact]
    public async Task Another_owner_cannot_update_business()
    {
        var ownerA = await CreateOwnerWithBusinessAsync("09121000011", "Protected Shop");
        var ownerB = await CreateOwnerWithBusinessAsync("09121000012", "Other Shop");

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/Business/{ownerA.BusinessId}",
            ownerB.AccessToken,
            ValidBusinessPayload("Hijacked", "Nowhere"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var detail = await ReadAsync<BusinessDto>(
            await _client.GetAsync($"/api/Business/{ownerA.BusinessId}"));
        Assert.Equal("Protected Shop", detail.Name);
        Assert.Equal(ownerA.UserId, detail.OwnerId);
    }

    [Fact]
    public async Task Customer_cannot_update_business()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000013", "Owner Shop");
        var customer = await RegisterCustomerAsync("09121000014");

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/Business/{owner.BusinessId}",
            customer.AccessToken,
            ValidBusinessPayload("Customer Edit", "Nowhere"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_with_null_required_fields_is_rejected()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000015", "Valid Shop");

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/Business/{owner.BusinessId}")
        {
            Content = new StringContent(
                """{"name":null,"city":null,"address":null,"description":null}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_does_not_allow_changing_owner_id_through_payload()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000016", "Owned Shop");
        var other = await RegisterCustomerAsync("09121000017");

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/Business/{owner.BusinessId}")
        {
            Content = new StringContent(
                $$"""{"name":"Still Mine","city":"Tehran","address":"Addr","description":null,"ownerId":"{{other.User.UserId}}","id":999}""",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<UpdateBusinessResponse>(response);
        Assert.Equal(owner.UserId, body.Data.OwnerId);
        Assert.Equal(owner.BusinessId, body.Data.Id);
        Assert.Equal("Still Mine", body.Data.Name);
    }

    [Fact]
    public async Task Update_nonexistent_business_does_not_succeed()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000018", "Existing Shop");

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            "/api/Business/999999",
            owner.AccessToken,
            ValidBusinessPayload("Ghost", "Nowhere"));

        // Current implementation checks ownership before null and returns Forbid for missing rows.
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected Forbid or NotFound, got {response.StatusCode}");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Business deletion ---

    [Fact]
    public async Task Admin_can_delete_business()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000019", "Delete Me");
        var admin = await CreateAdminAsync("09121000020");

        var response = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/Business/{owner.BusinessId}",
            admin.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await _client.GetAsync($"/api/Business/{owner.BusinessId}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task Owner_cannot_delete_business()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000021", "Keep Me");

        var response = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/Business/{owner.BusinessId}",
            owner.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var detail = await _client.GetAsync($"/api/Business/{owner.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_delete_business()
    {
        var owner = await CreateOwnerWithBusinessAsync("09121000022", "Keep Me Too");
        var customer = await RegisterCustomerAsync("09121000023");

        var response = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/api/Business/{owner.BusinessId}",
            customer.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_delete_returns_unauthorized()
    {
        var response = await _client.DeleteAsync("/api/Business/1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_delete_nonexistent_business_returns_not_found()
    {
        var admin = await CreateAdminAsync("09121000024");

        var response = await SendAuthorizedAsync(
            HttpMethod.Delete,
            "/api/Business/999999",
            admin.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Authorization / security ---

    [Fact]
    public async Task Changing_business_id_in_url_cannot_bypass_ownership_on_update()
    {
        var ownerA = await CreateOwnerWithBusinessAsync("09121000025", "A Shop");
        var ownerB = await CreateOwnerWithBusinessAsync("09121000026", "B Shop");

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/Business/{ownerA.BusinessId}",
            ownerB.AccessToken,
            ValidBusinessPayload("Bypass Attempt", "Hack City"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var detailA = await ReadAsync<BusinessDto>(
            await _client.GetAsync($"/api/Business/{ownerA.BusinessId}"));
        Assert.Equal("A Shop", detailA.Name);
    }

    [Fact]
    public async Task Forbidden_update_response_does_not_expose_exception_details()
    {
        var ownerA = await CreateOwnerWithBusinessAsync("09121000027", "Secret Shop");
        var ownerB = await CreateOwnerWithBusinessAsync("09121000028", "Other");

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/api/Business/{ownerA.BusinessId}",
            ownerB.AccessToken,
            ValidBusinessPayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", content, StringComparison.OrdinalIgnoreCase);
    }

    // --- Helpers ---

    private static object ValidBusinessPayload(
        string name = "Test Business",
        string city = "Tehran") =>
        new
        {
            name,
            city,
            address = "Test Address 1",
            description = "Test description"
        };

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
        return (await ReadAsync<AuthResponse>(response));
    }

    private async Task<HttpResponseMessage> RegisterBusinessAsync(
        string accessToken,
        string name)
    {
        return await SendAuthorizedAsync(
            HttpMethod.Post,
            "/api/Business/register",
            accessToken,
            new
            {
                name,
                city = "Tehran",
                address = "Owner Address",
                description = "Owner business"
            });
    }

    private async Task<OwnerContext> CreateOwnerWithBusinessAsync(
        string phoneNumber,
        string businessName)
    {
        var customer = await RegisterCustomerAsync(phoneNumber);
        var registerResponse = await RegisterBusinessAsync(customer.AccessToken, businessName);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await ReadAsync<AuthResponse>(registerResponse);
        var mine = await GetMineAsync(auth.AccessToken);

        Assert.True(mine.HasBusiness);
        Assert.NotNull(mine.Business);

        return new OwnerContext(auth.User.UserId, auth.AccessToken, mine.Business!.Id);
    }

    private async Task<AdminContext> CreateAdminAsync(string phoneNumber)
    {
        var customer = await RegisterCustomerAsync(phoneNumber);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        const string adminRole = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            var created = await roleManager.CreateAsync(new IdentityRole(adminRole));
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
        }

        var user = await userManager.FindByIdAsync(customer.User.UserId);
        Assert.NotNull(user);
        var roleResult = await userManager.AddToRoleAsync(user!, adminRole);
        Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(e => e.Description)));

        var login = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            phoneNumber,
            password = "Password1"
        });
        login.EnsureSuccessStatusCode();
        var auth = await ReadAsync<AuthResponse>(login);

        return new AdminContext(customer.User.UserId, auth.AccessToken);
    }

    private async Task<MyBusinessResponse> GetMineAsync(string accessToken)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/Business/mine", accessToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<MyBusinessResponse>(response);
    }

    private async Task<SessionResponse> GetSessionAsync(string accessToken)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Get, "/api/Auth/session", accessToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<SessionResponse>(response);
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
    private sealed record SessionResponse(bool Authenticated, UserResponse User, string[] Roles);
    private sealed record BusinessDto(
        int Id,
        string Name,
        string City,
        string Address,
        string? Description,
        string? ProfileImageUrl,
        string OwnerId);
    private sealed record MyBusinessResponse(bool HasBusiness, BusinessDto? Business);
    private sealed record UpdateBusinessResponse(string Message, BusinessDto Data);
    private sealed record OwnerContext(string UserId, string AccessToken, int BusinessId);
    private sealed record AdminContext(string UserId, string AccessToken);
}
