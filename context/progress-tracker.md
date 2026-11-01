# Backend progress

**Last updated:** 2026-11-02  
**Current task:** Phase 9 — Booking Details API (`feature-spec/10-booking-detail-endpoint.md`) — **Complete**  
**Prior:** Phase 8 — Booking Listing, Filtering, Search & Pagination — complete  
**Docs:** Agent context suite added (`AGENTS.md`, overview, architecture, standards, workflow, issues)

---

# Phase 9 — Booking Details API

Spec: `context/feature-spec/10-booking-detail-endpoint.md`

| Area | Status |
|------|--------|
| `GET /api/bookings/{bookingId}` | Done |
| Roles: `BusinessOwner`, `Employee` (Customer/anonymous rejected) | Done |
| Owner scoped to owned business; employee scoped to own `EmployeeId` | Done |
| `BookingDetailsDto` with nested customer/service/employee/business | Done |
| AsNoTracking + DTO projection | Done |
| Integration tests (9) | Done |
| Schema migration | Not needed |
| Updates / delete / reschedule / customer history / notifications | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `Controllers/BookingController.cs` | `GET /api/bookings/{bookingId}` |
| `Infrastructure/Services/BookingService.Cs` | `GetByIdAsync` |
| `Application/Interfaces/IBookingService.cs` | GetById contract |
| `Application/Dtos/Booking/BookingDetailsDto.cs` | Nested details DTO |
| `tests/.../BookingDetailTests.cs` | Integration coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingDetailTests" --verbosity minimal
```

Result: **9 total — 9 passed**. Docker required.

---

# Phase 8 — Booking Listing, Filtering, Search & Pagination

Spec: `context/feature-spec/09-bookins-api-complete.md`

| Area | Status |
|------|--------|
| `GET /api/bookings` | Done |
| Roles: `BusinessOwner`, `Employee` (Customer/anonymous rejected) | Done |
| Owner scoped to owned business; employee scoped to own `EmployeeId` | Done |
| Filters: status, employeeId, customerId, serviceId, fromDate, toDate, search | Done |
| Pagination via existing `PagedResult<T>` (+ `hasPreviousPage` / `hasNextPage`) | Done |
| Sort: `StartTime` desc, `Id` desc | Done |
| FluentValidation on query DTO | Done |
| Integration tests (22) | Done |
| Schema migration | Not needed |
| Customer history / create/status/delete/notifications | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `Controllers/BookingController.cs` | `GET /api/bookings` |
| `Infrastructure/Services/BookingService.Cs` | `ListAsync` filtered/paged query |
| `Application/Interfaces/IBookingService.cs` | List contract |
| `Application/Dtos/Booking/BookingListQuery.cs` | Query params |
| `Application/Dtos/Booking/BookingListItemDto.cs` | List row DTO |
| `Application/Validators/Booking/BookingListQueryValidator.cs` | FluentValidation |
| `Application/Common/PagedResult.cs` | Added hasPrevious/hasNext |
| `tests/.../BookingListTests.cs` | Integration coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingListTests" --verbosity minimal
```

Result: **22 total — 22 passed**. Docker required.

---

# Phase 8 — Automatic Pending Booking Expiration

Spec: `context/feature-spec/08-pending-auto-exipration.md`

| Area | Status |
|------|--------|
| `PendingBookingExpirationService` (`BackgroundService`) | Done |
| Atomic `ExecuteUpdateAsync` Pending→Expired by `CreatedAt` | Done |
| Config: `Booking:PendingExpirationMinutes` (default 30) | Done |
| Config: `Booking:ExpirationPollingIntervalMinutes` (default 1) | Done |
| Multi-instance safe (single SQL UPDATE, no distributed lock) | Done |
| Integration tests (11) | Done |
| Schema migration | Not needed |
| Notifications / status APIs / list endpoints | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `Infrastructure/Services/PendingBookingExpirationService.cs` | Hosted poller |
| `Infrastructure/Services/BookingService.Cs` | `ExpirePendingBookingsAsync` |
| `Application/Interfaces/IBookingService.cs` | Expire contract |
| `Infrastructure/.../BookingSettings.cs` | Expiration + poll settings |
| `Program.cs` | `AddHostedService` |
| `appsettings.json` | Config defaults |
| `tests/.../BookingPendingExpirationTests.cs` | PostgreSQL coverage |
| `tests/.../AuthTests.cs` | Disable hosted service in Testcontainers host |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingPendingExpirationTests" --verbosity minimal
```

Result: **11 total — 11 passed**. Docker required.

---

# Phase 6 — Booking Lifecycle & Status Management

Spec: `context/feature-spec/06-status-management.md`

| Area | Status |
|------|--------|
| `PATCH /api/bookings/{bookingId}/status` | Done |
| Owner-only (`BusinessOwner` + ownership check) | Done |
| Transitions: Pending→Confirmed/Cancelled; Confirmed→Completed/Cancelled | Done |
| Terminal: Cancelled / Completed / Expired; Expired not assignable | Done |
| Enum validation via FluentValidation `IsInEnum` (numeric JSON enums) | Done |
| Integration tests (14) | Done |
| Expiration BackgroundService / notifications / customer cancel | Out of scope |
| Schema migration | Not needed |

## Files (this phase)

| Path | Role |
|------|------|
| `Controllers/BookingController.cs` | Status PATCH endpoint |
| `Infrastructure/Services/BookingService.Cs` | Ownership + transition rules |
| `Application/Interfaces/IBookingService.cs` | `UpdateStatusAsync` |
| `Application/Dtos/Booking/UpdateBookingStatusDto.cs` | Request DTO |
| `Application/Validators/Booking/UpdateBookingStatusDtoValidator.cs` | FluentValidation |
| `tests/.../BookingStatusManagementTests.cs` | Integration coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingStatusManagementTests" --verbosity minimal
```

Result: **14 total — 14 passed**. Docker required.

---

# Phase 5 — Booking Concurrency Protection

Spec: `context/feature-spec/06-bookinh-concurrency.md`

| Area | Status |
|------|--------|
| Strategy: PostgreSQL EXCLUDE (gist) on active bookings | Done |
| `btree_gist` + `EX_Bookings_Employee_Active_Time_Overlap` | Done (migration created, **not applied**) |
| Half-open `tstzrange(..., '[)')`; Pending+Confirmed only | Done |
| Create flows wrap customer+booking in a transaction | Done |
| Exclusion → Persian `409` (`InvalidOperationException`) | Done |
| Status transition APIs | None exist — DB constraint still guards UPDATEs |
| Integration tests (10) | Done |
| Expiration BackgroundService / lifecycle APIs | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `Migrations/20261101201947_BookingActiveOverlapExclusion.cs` | `btree_gist` + exclusion constraint |
| `Infrastructure/Services/BookingService.Cs` | Transaction + exclusion catch on create |
| `tests/.../BookingConcurrencyTests.cs` | Overlap / adjacent / concurrent coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingConcurrencyTests" --verbosity minimal
```

Result: **10 total — 10 passed** (includes concurrent overlap + different-employee cases). Docker required.

## Strategy note

EF Core / Npgsql 10 do not model arbitrary `EXCLUDE` constraints; constraint is raw SQL in the migration. Application-only check-then-insert was rejected.

---

# Phase 4 — Booking Availability API

Spec: `context/feature-spec/05.booking-avalbility.md`

| Area | Status |
|------|--------|
| `GET /api/bookings/availability` | Done |
| Query: businessId, serviceId, employeeId, date | Done |
| Response `AvailabilitySlotDto` (DateTimeOffset ISO with business offset) | Done |
| Working hours = employee + date-specific intervals | Done |
| Occupancy: Pending + Confirmed; Cancelled/Expired/Completed free | Done |
| Half-open `[start, end)` overlap | Done |
| Past slots excluded | Done |
| Slot interval via `Booking:SlotIntervalMinutes` (default 15) | Done |
| Timezone via `Business.TimeZoneId` (no hardcoded Tehran in calc) | Done |
| Integration tests (12) | Done |
| Overlap protection / expiration / status APIs | Out of scope |
| Schema migration | Not needed |

## Files (this phase)

| Path | Role |
|------|------|
| `Controllers/BookingController.cs` | Availability endpoint |
| `Infrastructure/Services/BookingService.Cs` | Slot generation |
| `Application/Interfaces/IBookingService.cs` | `GetAvailabilityAsync` |
| `Application/Dtos/Booking/AvailabilityQueryDto.cs` | Query DTO |
| `Application/Dtos/Booking/AvailabilitySlotDto.cs` | Response DTO |
| `Application/Validators/Booking/AvailabilityQueryDtoValidator.cs` | FluentValidation |
| `Infrastructure/.../BookingSettings.cs` | Slot interval config |
| `appsettings.json` / `Program.cs` | Config + DI |
| `tests/.../BookingAvailabilityTests.cs` | Integration coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingAvailabilityTests" --verbosity minimal
```

Result: **12 total — 12 passed**. Docker required.

## WorkingHour limitations (unchanged model)

- Employee-specific only (no business-wide hours).
- Date-specific rows only (no weekly recurring template).
- Multiple intervals per date allowed; no dedicated break entity (gaps between intervals act as breaks).

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

1. Apply pending booking migrations when ready (`BookingDomainFoundation`, `BookingActiveOverlapExclusion` — only on request).
2. Later booking phases: customer self-cancel / mine history — only when specified.
