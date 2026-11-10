using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure.Persistence;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    var fromArray = configuration.GetSection("Frontend:Origins").Get<string[]>();
    if (fromArray is { Length: > 0 })
    {
        return fromArray
            .Select(o => o?.Trim().TrimEnd('/'))
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    var csv = configuration["Frontend:Origins"];
    if (!string.IsNullOrWhiteSpace(csv))
    {
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'))
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

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
var secretKey = jwtSettings["Key"]!;

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
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication(); // باید قبل از UseAuthorization بیاد
app.UseAuthorization();
app.MapControllers();
app.Run();