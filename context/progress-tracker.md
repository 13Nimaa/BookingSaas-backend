# Backend progress

**Last updated:** 2026-11-01  
**Current task:** Phase 3 — Booking Creation API (`feature-spec/04-booking=creation-API.md`) — **Complete**  
**Prior:** Phase 2 — Employee-Service Management API — complete  
**Docs:** Agent context suite added (`AGENTS.md`, overview, architecture, standards, workflow, issues)

---

# Agent context suite

| Area | Status |
|------|--------|
| Root + `context/AGENTS.md` | Reading order for agents | Done |
| `project-overview.md` | Product goals / scope | Done |
| `architecture-context.md` | Layers + invariants | Done |
| `code-standards.md` | Backend conventions | Done |
| `ai-workflow-rules.md` | Scoping + doc sync | Done |
| `current-issues.md` | Open debt / findings | Done |
| `progress-tracker.md` | This file (frontend-style) | Done |

Existing docs kept: `PROJECT_INVENTORY.md`, `feature-spec/*`, API frontend notes, test notes.

---

# Phase 3 — Booking Creation API

Spec: `context/feature-spec/04-booking=creation-API.md`

| Area | Status |
|------|--------|
| `POST /api/bookings` (authenticated) | Done |
| `POST /api/bookings/guest` (anonymous) | Done |
| Shared selection validation (business/service/employee/assignment) | Done |
| EndTime = StartTime + Service.DurationMinutes (client EndTime ignored) | Done |
| CreatorType / CreatedByUserId server-side | Done |
| `BookingDto` response shape | Done |
| FluentValidation (CreateBooking / CreateGuestBooking) | Done |
| `IBookingService` DI registration | Done |
| Guest phone must not link to registered customer account | Done (`GetOrCreateForGuestAsync` guests-only) |
| Integration tests (9) | Done |
| Availability / overlap / expiration / owner create | Out of scope |
| Schema migration | Not needed |

## Files (this phase)

| Path | Role |
|------|------|
| `Controllers/BookingController.cs` | Authenticated + guest create |
| `Infrastructure/Services/BookingService.Cs` | Validation, EndTime calc, creator fields |
| `Infrastructure/Services/CustomerService.cs` | Guest lookup ignores registered customers |
| `Application/Dtos/Booking/BookingDto.cs` | Response DTO |
| `Application/Validators/Booking/*` | FluentValidation |
| `Program.cs` | `IBookingService` registration |
| `tests/.../BookingCreationTests.cs` | Integration coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingCreationTests" --verbosity minimal
```

Result: **9 total — 9 passed**. Docker required.

---

# Phase 2 — Employee-Service Management API

Spec: `context/feature-spec/03-employee-connection-to-service.md`

| Area | Status |
|------|--------|
| `GET /api/employees/{employeeId}/services` | Done |
| `POST /api/employees/{employeeId}/services` | Done |
| `DELETE /api/employees/{employeeId}/services/{serviceId}` | Done |
| Ownership via authenticated owner + employee.BusinessId | Done |
| Same-business check on assign | Done |
| Duplicate prevention (app check + PK + unique-violation catch) | Done |
| Block remove when Booking history exists for pair | Done |
| FluentValidation on `AssignServiceDto` | Done |
| Integration tests (6) | Done |
| EmployeeServices schema migration | Not needed (composite PK already exists) |
| BookingController / BookingService changes | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `Controllers/EmployeeServicesController.cs` | List / assign / remove |
| `Application/Dtos/Employee/AssignServiceDto.cs` | Request body |
| `Application/Validators/Employee/AssignServiceDtoValidator.cs` | FluentValidation |
| `tests/.../EmployeeServicesTests.cs` | Integration coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~EmployeeServicesTests" --verbosity minimal
```

Result: **6 total — 6 passed**. Docker required.

---

# Phase 1 — Booking domain foundation

Spec: `context/feature-spec/02-booking-foundation.md`

| Area | Status |
|------|--------|
| `BookingStatus.Expired` appended (preserve int values) | Done |
| `BookingCreatorType` + `CreatorType` / `CreatedByUserId` / `CreatedByUser` | Done |
| `Business.TimeZoneId` required, default `Asia/Tehran` | Done |
| `BookingConfiguration` (Restrict on Business/Customer/Employee/Service; SetNull on CreatedByUser; employee/time indexes) | Done |
| Auto-register via `ApplyConfigurationsFromAssembly` | Done |
| EF migration `20261031213454_BookingDomainFoundation` | Done (created, **not applied**) |
| `dotnet build` verification | Done |
| BookingController / availability / BackgroundService / creator assignment in services | Out of scope (controller + creator done in Phase 3) |

## Timezone decision

Project is Iran-focused. `TimeZoneId` is required with default / DB default `Asia/Tehran`.

---

# Business integration tests — previously complete

| Area | Status |
|------|--------|
| Auth fixture reuse + FakeImageUploadService | Done |
| Business create / retrieve / update / delete / authz tests | Done |
| Production business code unchanged | Done |

## Test run (historical)

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --verbosity minimal
```

Result recorded: **30 total — 30 passed** (Auth 6, Business 24). Docker required.

Findings from that pass live in `current-issues.md` (Business API + Auth sections).

---

# Customer CRM (`feature-spec/01-complete-customers.md`) — previously complete

CRM fields, validators, list/query, soft-active behavior — see inventory. Spec folder retained for history.

---

# Next

1. Apply `BookingDomainFoundation` migration when ready (`dotnet ef database update` — only on request).
2. Later booking phases: availability, overlap/exclusion, expiration BackgroundService, owner approval/status transitions — only when specified.
