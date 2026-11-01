# Architecture Context

## Stack

| Layer | Technology | Role |
| ----- | ---------- | ---- |
| Host | ASP.NET Core (.NET 10) | HTTP API, DI, middleware |
| Auth | Identity + JWT Bearer | Users, roles, tokens |
| Persistence | EF Core + Npgsql | PostgreSQL mappings & migrations |
| Validation | FluentValidation | DTO rules (auto-validation) |
| Media | Cloudinary | Profile / business images |
| Tests | xUnit + Testcontainers | Integration tests against real Postgres |

## System Boundaries

| Area | Path | Responsibility |
| ---- | ---- | -------------- |
| Controllers | `Controllers/` | HTTP surface, auth attributes, thin orchestration |
| Domain | `Domain/Entities/` | Entities + enums only |
| Application contracts | `Application/Dtos/` | Request/response shapes |
| Application ports | `Application/Interfaces/` | Service abstractions |
| Application rules | `Application/Validators/` | FluentValidation validators |
| Application errors | `Application/Exceptions/` | Domain/app exceptions |
| Persistence | `Infrastructure/Persistence/` | `AppDbContext`, EF configurations |
| Services | `Infrastructure/Services/` | Service implementations |
| Migrations | `Migrations/` | EF migrations + model snapshot |
| Tests | `tests/BookingSaas-backend.Tests/` | Integration tests |
| Specs / agent context | `context/` | Inventory, progress, rules, feature specs |

## Request Model

- Controllers accept DTOs and return API results; keep business logic in services when a service already exists for that domain.
- Authorization is enforced on the backend (roles / ownership). Frontend role checks are UX only.
- Phone number is the login identity (`UserName = PhoneNumber`).
- JSON uses camelCase for clients; dates are UTC ISO-8601.

## Domain Model Notes

- **Canonical booking model is `Booking`.** Do not create a second booking entity.
- **`Appointment` is legacy / unused by API.** Do not modify or delete it unless a dedicated migrate/remove task says so.
- One owner → one business (`Business.Owner` / `ApplicationUser.OwnedBusiness`).
- Customers are per-business; guests have `UserId = null`.
- Identity roles (`Customer`, `BusinessOwner`, `Employee`, `Admin`) are authoritative. `ApplicationUser.Role` enum is legacy/unused by controllers.

## EF Configuration

- Fluent configs live in `Infrastructure/Persistence/Configurations/`.
- `AppDbContext.OnModelCreating` calls `ApplyConfigurationsFromAssembly` — new `IEntityTypeConfiguration<T>` types are auto-registered.
- Prefer `DeleteBehavior.Restrict` for history-bearing relationships (see `AppointmentConfiguration`, `BookingConfiguration`).
- Optional user FKs that should survive user deletion use `SetNull` (e.g. Customer.User, Booking.CreatedByUser).
- Create migrations with `dotnet ef migrations add …`. **Do not apply** (`database update`) unless explicitly asked.

## Auth Model

| Token | Lifetime / storage |
| ----- | ------------------ |
| Access JWT | ~15 minutes; claims include user id, phone, roles, `jti` |
| Refresh | 7 days; SHA-256 hashed in `RefreshTokens`; also HttpOnly cookie `refreshToken`; rotates on refresh |

Default register role: `Customer`. Business self-register adds `BusinessOwner`.

## Invariants

1. Preserve existing naming, folder layout, and layering — do not invent a new architecture style mid-feature.
2. Do not implement beyond the active feature-spec phase (e.g. Phase 1 booking foundation ≠ BookingController).
3. Do not dual-write `Booking` and `Appointment`.
4. Do not reorder existing enum members stored as integers in PostgreSQL; append only.
5. Inspect live entity/config/migration snapshot before changing relationships or delete behaviors.
6. If a required relationship differs from a spec/audit description, stop and ask — do not guess.
7. Secrets (`Jwt:Key`, Cloudinary, connection strings beyond local defaults) stay out of committed config where already treated as secrets.
8. Prefer small, verifiable increments; update `progress-tracker.md` after meaningful work.
9. For a given employee, Pending/Confirmed bookings must not overlap on half-open `[StartTime, EndTime)`; enforced by Postgres exclusion constraint `EX_Bookings_Employee_Active_Time_Overlap` (not app-only check-then-insert).
