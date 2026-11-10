using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure.Persistence;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Infrastructure.Services;
using Application.Interfaces;
using Application.Validators.Customer;
using BookingSaas_backend.Application.Interfaces;
using BookingSaas_backend.Application.Services;
using BookingSaas_backend.Infrastructure.Configuration;
using BookingSaas_backend.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway / reverse proxies terminate TLS; honor X-Forwarded-* headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Railway edge IPs are not fixed; clear known networks/proxies.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("Cloudinary"));
builder.Services.Configure<BookingSettings>(
    builder.Configuration.GetSection(BookingSettings.SectionName));
// ---------- DbContext (Postgres) ----------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Identity ----------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ---------- CORS (frontend calls the API directly, no BFF) ----------
// Origins are configurable via "Frontend:Origins" (see appsettings.json).
// Railway: prefer Frontend__Origins__0 / __1, or a single comma-separated
// Frontend__Origins=https://a.com,https://b.com
// Must be explicit origins (no AllowAnyOrigin) because the frontend sends
// credentials ("credentials: include") and an Authorization header.
static string[] ResolveFrontendOrigins(IConfiguration configuration)
{
    static IEnumerable<string> Normalize(IEnumerable<string?> values) =>
        values
            .Select(o => o?.Trim().TrimEnd('/'))
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);

    // Prefer explicit Railway-friendly single var when set.
    var single = configuration["FRONTEND_ORIGIN"] ?? configuration["Frontend:Origin"];
    if (!string.IsNullOrWhiteSpace(single))
        return Normalize(single.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

    var fromArray = configuration.GetSection("Frontend:Origins").Get<string[]>();
    if (fromArray is { Length: > 0 })
        return Normalize(fromArray).ToArray();

    var csv = configuration["Frontend:Origins"];
    if (!string.IsNullOrWhiteSpace(csv))
        return Normalize(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

    return ["http://localhost:3000"];
}

var frontendOrigins = ResolveFrontendOrigins(builder.Configuration);
Console.WriteLine($"CORS origins: {string.Join(", ", frontendOrigins)}");

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(frontendOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ---------- JWT Authentication ----------
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Key"];
if (string.IsNullOrWhiteSpace(secretKey))
    throw new InvalidOperationException(
        "Jwt:Key is missing. Set Railway variable Jwt__Key (or User Secrets locally).");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing. Set Railway variable ConnectionStrings__DefaultConnection.");

Console.WriteLine("Startup config OK: Jwt:Key present, DefaultConnection present.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

// ---------- Controllers + OpenAPI ----------
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerDtoValidator>();
builder.Services.AddOpenApi();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IBusinessFavoriteService, BusinessFavoriteService>();
builder.Services.AddScoped<ICustomerDashboardService, CustomerDashboardService>();
builder.Services.AddScoped<IImageUploadService, CloudinaryImageUploadService>();
builder.Services.AddHostedService<PendingBookingExpirationService>();
var app = builder.Build();

// ---------- Middleware pipeline ----------
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Local only — Railway terminates HTTPS at the proxy; redirecting
    // OPTIONS preflight here strips CORS headers and shows up as a CORS error.
    app.UseHttpsRedirection();
}

// CORS before auth so preflight OPTIONS is answered without JWT.
app.UseCors("Frontend");

app.UseAuthentication(); // باید قبل از UseAuthorization بیاد
app.UseAuthorization();
app.MapControllers();
app.Run();