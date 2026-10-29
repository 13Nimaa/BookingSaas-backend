using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingSaas_backend.Application.Interfaces;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace BookingSaas_backend.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthCollection : ICollectionFixture<AuthFixture>
{
    public const string Name = "authentication";
}

[Collection(AuthCollection.Name)]
public sealed class AuthTests : IAsyncLifetime
{
    private readonly AuthFixture _fixture;
    private HttpClient _client = null!;

    public AuthTests(AuthFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        _client = _fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
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
    public async Task Register_with_valid_input_succeeds_and_assigns_customer_role()
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/register", new
        {
            name = "Test Customer",
            phoneNumber = "09120000001",
            password = "Password1",
            confirmPassword = "Password1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal("Test Customer", body.User.Name);

        var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/Auth/session");
        sessionRequest.Headers.Authorization = new("Bearer", body.AccessToken);
        var sessionResponse = await _client.SendAsync(sessionRequest);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = await sessionResponse.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.Contains("Customer", session!.Roles);
    }

    [Fact]
    public async Task Registering_same_phone_twice_returns_conflict()
    {
        var request = new
        {
            name = "Duplicate Test",
            phoneNumber = "09120000002",
            password = "Password1",
            confirmPassword = "Password1"
        };

        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync("/api/Auth/register", request)).StatusCode);

        var duplicate = await _client.PostAsJsonAsync("/api/Auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Login_with_valid_credentials_succeeds()
    {
        await RegisterAsync("09120000003", "Password1");

        var response = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            phoneNumber = "09120000003",
            password = "Password1"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Login_with_incorrect_password_returns_unauthorized()
    {
        await RegisterAsync("09120000004", "Password1");

        var response = await _client.PostAsJsonAsync("/api/Auth/login", new
        {
            phoneNumber = "09120000004",
            password = "WrongPassword1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_unauthorized()
    {
        var response = await _client.PostAsync("/api/Auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_and_refresh_cannot_reuse_it()
    {
        var registration = await RegisterAsync("09120000005", "Password1");

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/logout");
        logout.Headers.Add("Cookie", $"refreshToken={registration.RefreshToken}");
        var logoutResponse = await _client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/refresh");
        refresh.Headers.Add("Cookie", $"refreshToken={registration.RefreshToken}");
        var refreshResponse = await _client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private async Task<AuthResponse> RegisterAsync(string phoneNumber, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/Auth/register", new
        {
            name = "Test User",
            phoneNumber,
            password,
            confirmPassword = password
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private sealed record AuthResponse(UserResponse User, string AccessToken, string RefreshToken);
    private sealed record UserResponse(string UserId, string Name, string PhoneNumber);
    private sealed record SessionResponse(bool Authenticated, UserResponse User, string[] Roles);
}

public sealed class AuthFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("bookingsaas_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    public WebApplicationFactory<Controllers.AuthController> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Controllers.AuthController>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("Jwt:Key", "test-only-signing-key-at-least-32-characters-long");
                builder.UseSetting("Jwt:Issuer", "BookingSaas.Tests");
                builder.UseSetting("Jwt:Audience", "BookingSaas.Tests");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Key"] = "test-only-signing-key-at-least-32-characters-long",
                        ["Jwt:Issuer"] = "BookingSaaS.Tests",
                        ["Jwt:Audience"] = "BookingSaaS.Tests"
                    });
                });
                builder.ConfigureServices(services =>
                {
                    var existing = services
                        .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                                    d.ServiceType == typeof(AppDbContext) ||
                                    d.ServiceType == typeof(IImageUploadService))
                        .ToList();

                    foreach (var descriptor in existing)
                        services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString()));
                    services.AddSingleton<IImageUploadService, FakeImageUploadService>();
                });
            });

        await ResetDatabaseAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Factory?.Services.CreateAsyncScope() ??
                                throw new InvalidOperationException("Test factory is not initialized.");
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        await _postgres.DisposeAsync();
    }
}

internal sealed class FakeImageUploadService : IImageUploadService
{
    public Task<ImageUploadResponse> UploadAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImageUploadResponse(
            "https://test.local/image.jpg",
            "test-public-id"));

    public Task DeleteAsync(
        string publicId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
