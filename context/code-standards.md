# Code Standards

## General

- Keep modules small and single-purpose.
- Fix root causes — do not layer workarounds.
- Do not mix unrelated domains in one change.
- Respect boundaries in `architecture-context.md`.
- Match existing naming (including legacy spellings in filenames) unless the task is an explicit rename.

## C# / ASP.NET

- Nullable reference types are enabled — honor them.
- Prefer primary constructors / records for DTOs when that is already the local pattern (`sealed record` DTOs).
- Controllers stay thin; put reusable domain operations in `Infrastructure/Services` behind interfaces.
- Use existing response / exception patterns already used by nearby controllers.
- Do not add new NuGet packages unless approved.

## Domain & EF

- Entities live in `Domain/Entities/`; enums colocated with their primary entity when that is existing style (`BookingStatus` with `Booking`).
- Fluent API in `Infrastructure/Persistence/Configurations/*Configuration.cs`.
- Append enum members only; never reorder or renumber stored ints.
- Migrations: one focused migration per phase/task; inspect for destructive drops before finishing.
- Do not run `dotnet ef database update` unless explicitly requested.

## Validation

- FluentValidation validators under `Application/Validators/<Domain>/`.
- Register via assembly scan already wired in `Program.cs`.
- Mirror important DB max lengths on DTOs/validators when touching that surface.

## Auth & Authorization

- Use Identity role names: `Customer`, `BusinessOwner`, `Employee`, `Admin`.
- Enforce ownership on owner-scoped resources (business id / owner id checks).
- Do not treat `ApplicationUser.Role` enum as source of truth.

## API Contracts

- Prefer existing DTO folders under `Application/Dtos/<Domain>/`.
- Do not invent frontend-facing shapes without updating inventory / feature specs.
- Keep camelCase JSON compatible with the frontend client.

## File Organization

| Path | Contains |
| ---- | -------- |
| `Controllers/` | HTTP endpoints |
| `Domain/Entities/` | Entities + enums |
| `Application/Dtos/` | DTOs |
| `Application/Interfaces/` | Service ports |
| `Application/Validators/` | FluentValidation |
| `Infrastructure/Persistence/` | DbContext + configurations |
| `Infrastructure/Services/` | Service implementations |
| `Migrations/` | EF migrations |
| `tests/` | Integration tests |
| `context/` | Agent/product docs |

## Testing

- Integration tests live in `tests/BookingSaas-backend.Tests/`.
- Reuse `AuthFixture` / Testcontainers patterns; Docker must be running.
- Do not add tests unless the task or user explicitly asks.
- Never commit real production secrets into tests.

## Protected / Sensitive Areas

- Do not casually rewrite Identity / JWT bootstrap in `Program.cs`.
- Do not delete or “clean up” `Appointment` while booking work is in flight.
- Do not apply migrations or mutate Docker DB contents unless asked.
