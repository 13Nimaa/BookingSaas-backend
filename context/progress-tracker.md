# Backend progress

**Last updated:** 2026-11-04  
**Current task:** Business public list — search / city filter / sort / pagination — **Complete**  
**Prior:** Phase 5 — Integration & Regression Testing (employee-specific price/duration) — complete  
**Docs:** Agent context suite added (`AGENTS.md`, overview, architecture, standards, workflow, issues)

---

# Business public list — search, filter, sort, pagination

| Area | Status |
|------|--------|
| `GET /api/Business` returns `PagedResult<BusinessDto>` | Done |
| `search` on name/city/address/description | Done |
| `city` exact (case-insensitive) filter | Done |
| `sortBy` name\|city\|id + `sortOrder` | Done |
| FluentValidation on query | Done |
| Integration tests | Done |

## Files

| Path | Role |
|------|------|
| `Application/Dtos/Business/BusinessListQuery.cs` | Query params |
| `Application/Validators/Business/BusinessListQueryValidator.cs` | Validation |
| `Controllers/BusinessController.cs` | GetAll |
| `tests/.../BussinessTests.cs` | Coverage |

---

# Phase 5 — Integration & Regression (employee-specific price/duration)

Spec: `context/feature spec/17- Integration & Regression Testing.md`

| Area | Status |
|------|--------|
| E2E chain: assign → availability → override → auth/guest book → snapshot stability | Verified (new test) |
| Cross-feature consistency (overrides / availability / create / details) | Verified — no bugs found |
| Regression suites (ES, avail, create, concurrency, status, expire, detail) | **102 passed** |
| Frontend contract docs (`Booking-MVP-frontend.md`) | Updated |
| Migration `BookingPriceSnapshot` reviewed | Safe nullable add; **not applied** |
| Frontend implementation | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `tests/.../EmployeeServicePricingIntegrationTests.cs` | Chained E2E + omit/null PUT check |
| `context/Booking-MVP-frontend.md` | FE contracts for overrides / price snapshot |
| `context/feature spec/17-…md` | Spec saved |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~EmployeeServicePricingIntegrationTests|FullyQualifiedName~EmployeeServicesTests|FullyQualifiedName~BookingAvailabilityTests|FullyQualifiedName~BookingCreationTests|FullyQualifiedName~BookingConcurrencyTests|FullyQualifiedName~BookingStatusManagementTests|FullyQualifiedName~BookingPendingExpirationTests|FullyQualifiedName~BookingDetailTests" --verbosity minimal
```

Result: **102 total — 102 passed**.

## Migration apply (later, on request)

```bash
dotnet ef database update BookingPriceSnapshot --project BookingSaas-backend.csproj
```

Applies all pending migrations up through `BookingPriceSnapshot` (including `EmployeeServiceOverrides` if not yet applied).

---

# Phase 4 — Booking Integration with Employee-Specific Pricing & Duration

Spec: `context/feature spec/16- Booking Integration with Employee-Specific Pricing and Duration.md`

| Area | Status |
|------|--------|
| Create auth + guest use `DurationMinutesOverride ?? Service.DurationMinutes` | Done |
| Create auth + guest use `PriceOverride ?? Service.Price` | Done |
| `Booking.Price` snapshot column (nullable for legacy) | Done |
| Server-side `EndTime` from effective duration | Done |
| Detail DTO price from booking snapshot | Done |
| Exclusion / 409 concurrency preserved | Done |
| Migration `20261102130009_BookingPriceSnapshot` | Done (created, **not applied**) |
| Availability / Employee-Service contracts | Unchanged |
| Phase 5 regression suite | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `Domain/Entities/Booking.cs` | `Price` snapshot |
| `Infrastructure/Persistence/Configurations/BookingConfiguration.cs` | `decimal(10,2)` |
| `Infrastructure/Services/BookingService.Cs` | Resolve assignment + effective values |
| `Application/Dtos/Booking/BookingDto.cs` | Expose `Price` |
| `Application/Dtos/Booking/BookingDetailsDto.cs` | Snapshot `Price` |
| `Controllers/BookingController.cs` | Map `Price` |
| `Migrations/20261102130009_BookingPriceSnapshot.cs` | Add nullable `Price` |
| `tests/.../BookingCreationTests.cs` | Duration/price/conflict coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingCreationTests|FullyQualifiedName~BookingDetailTests|FullyQualifiedName~BookingConcurrencyTests" --verbosity minimal
```

Result: **35 total — 35 passed** (Creation 16, Detail 9, Concurrency 10). Migration **not applied**.

---

# Phase 3 — Availability Integration with Employee-Specific Duration

Spec: `context/feature spec/15- Availability Integration with Employee-Specific Service Duration.md`

| Area | Status |
|------|--------|
| Resolve `EffectiveDurationMinutes` from `EmployeeService` | Done |
| Availability slot generation uses effective duration | Done |
| Working-hours fit / overlap checks use effective duration | Done |
| Null override → `Service.DurationMinutes` | Done |
| Assignment required (not entity existence alone) | Done |
| Pending/Confirmed block; Cancelled/Completed/Expired free | Unchanged |
| Historical booking EndTime used as stored | Unchanged |
| Slot interval config unchanged | Done |
| Booking create / price / schema | Out of scope |
| Migration | Not in this phase |

## Files (this phase)

| Path | Role |
|------|------|
| `Infrastructure/Services/BookingService.Cs` | Availability duration from EmployeeService |
| `tests/.../BookingAvailabilityTests.cs` | Override + conflict coverage |
| `context/feature spec/15-…md` | Spec saved |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingAvailabilityTests" --verbosity minimal
```

Result: **18 total — 18 passed**. Docker required.

---

# Phase 2 — Employee-Service API Upgrade

Spec: `context/feature spec/14- Employee-Service API Upgrade.md`

| Area | Status |
|------|--------|
| GET returns default / override / effective values | Done |
| POST accepts optional `priceOverride` / `durationMinutesOverride` | Done |
| POST `{ serviceId }` only remains compatible | Done |
| PUT update + explicit-null reset (omit vs null) | Done |
| FluentValidation (price ≥ 0, precision 10,2; duration > 0) | Done |
| Ownership / duplicate / 404 rules preserved | Done |
| Schema migration | Not in this phase |
| Availability / booking resolution | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `Application/Dtos/Employee/EmployeeServiceDto.cs` | Response shape |
| `Application/Dtos/Employee/AssignServiceDto.cs` | Optional overrides on assign |
| `Application/Dtos/Employee/UpdateEmployeeServiceDto.cs` | Omit-vs-null PUT body |
| `Application/Validators/Employee/AssignServiceDtoValidator.cs` | Assign validation |
| `Application/Validators/Employee/UpdateEmployeeServiceDtoValidator.cs` | PUT validation |
| `Controllers/EmployeeServicesController.cs` | GET/POST/PUT/DELETE |
| `tests/.../EmployeeServicesTests.cs` | API coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~EmployeeServicesTests" --verbosity minimal
```

Result: **22 total — 22 passed**. Docker required.

---

# Phase 1 — Upgrade EmployeeService Domain Model

Spec: `context/feature specs/13-Upgrade EmployeeService Domain Model.md`

| Area | Status |
|------|--------|
| Explicit `EmployeeService` join entity | Done |
| `PriceOverride` nullable `decimal(10,2)` | Done |
| `DurationMinutesOverride` nullable `int` | Done |
| EF config preserves FKs / composite PK / Cascade | Done |
| Skip navigations `Employee.Services` / `Service.Employees` preserved | Done |
| Migration `20261102103531_EmployeeServiceOverrides` | Done (created, **not applied**) |
| Existing GET/POST/DELETE API unchanged | Done |
| Integration tests (11) | Done |
| PUT endpoint / availability / booking price-duration resolution | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `Domain/Entities/EmployeeService.cs` | Join entity + override columns |
| `Infrastructure/Persistence/Configurations/EmployeeServiceConfiguration.cs` | Fluent mapping |
| `Infrastructure/Persistence/Configurations/ServiceConfiguration.cs` | `UsingEntity<EmployeeService>()` |
| `Infrastructure/Persistence/AppDbContext.cs` | `DbSet<EmployeeService>` |
| `Migrations/20261102103531_EmployeeServiceOverrides.cs` | Add nullable override columns |
| `tests/.../EmployeeServicesTests.cs` | Override + duplicate coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~EmployeeServicesTests" --verbosity minimal
```

Result: **11 total — 11 passed**. Docker required. Migration **not applied**.

---

# Phase 11 — Booking MVP Integration & Production Readiness

Spec: `context/feature-spec/12-mvp-full-test.md`

Verification only — no feature code changes; no migrations applied to prod/local app DB.

| Area | Status |
|------|--------|
| E2E scenarios A–E (covered by existing integration tests; steps not one chained test) | Verified by test |
| Authorization rules (owner/employee/customer/guest) | Verified by test |
| API contract review | Inspected (warnings logged) |
| Migrations + Postgres EXCLUDE/`btree_gist` | Inspected (9 migrations; apply rights are a deploy concern) |
| Production config | Inspected (blockers below) |
| Full test suite | **141 passed / 0 failed** |
| Release build | **Succeeded** (NU1903 OpenAPI advisory) |

## Verdict

| Target | Result |
|--------|--------|
| Frontend integration | **Ready** |
| MVP deployment | **Not ready** — secrets/config + Postgres extension privileges |

## Blockers (deployment)

1. DB password committed in `appsettings.json` — rotate / move to env or secret store.
2. `Jwt:Key` not in checked-in config — must be supplied via User Secrets / env or startup fails.
3. Deploy role must allow `CREATE EXTENSION btree_gist` and EXCLUDE constraint from `BookingActiveOverlapExclusion`.

## Test gaps (non-blocking for FE)

- No single auth → availability → create → list → detail chain test.
- Authenticated customer create not asserted on owner/employee list responses (guest path is).
- `CustomerId` ↔ logged-in user not explicitly asserted on create.
- Expiration job then availability not chained (seeded Expired + create path are covered).

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --verbosity minimal
dotnet build BookingSaas-backend.csproj -c Release
```

Result: **141 total — 141 passed** (Docker / Testcontainers). Release: **0 errors**, NU1903 warning.

Booking-related counts: Creation 9, Availability 12, Concurrency 10, Expiration 11, Status 14, List 22, Detail 9, Mine 18 (+ EmployeeServices 6, Auth 6, Business 24).

---

# Phase 10 — Customer Booking History API

Spec: `context/feature-spec/11-Customer-Booking-History-API.md`

| Area | Status |
|------|--------|
| `GET /api/bookings/mine` | Done |
| Auth: any authenticated user; scope by `Customer.UserId` from JWT | Done |
| Multi-business Customer rows supported | Done |
| Guest bookings excluded (no phone matching) | Done |
| Filters: status, fromDate, toDate | Done |
| Pagination via existing `PagedResult<T>` | Done |
| Sort: `StartTime` desc, `Id` desc | Done |
| `CustomerBookingListItemDto` (business name; no customer CRM fields) | Done |
| FluentValidation on `BookingMineQuery` | Done |
| Integration tests (18) | Done |
| Schema migration | Not needed |
| Cancel / reschedule / guest lookup / notifications | Out of scope |

## Files (this phase)

| Path | Role |
|------|------|
| `Controllers/BookingController.cs` | `GET /api/bookings/mine` |
| `Infrastructure/Services/BookingService.Cs` | `ListMineAsync` |
| `Application/Interfaces/IBookingService.cs` | ListMine contract |
| `Application/Dtos/Booking/BookingMineQuery.cs` | Query params |
| `Application/Dtos/Booking/CustomerBookingListItemDto.cs` | Customer-facing list row |
| `Application/Validators/Booking/BookingMineQueryValidator.cs` | FluentValidation |
| `tests/.../BookingMineTests.cs` | Integration coverage |

## Test run

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --filter "FullyQualifiedName~BookingMineTests" --verbosity minimal
```

Result: **18 total — 18 passed**. Docker required.

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

1. Apply pending migrations when ready (`BookingDomainFoundation`, `BookingActiveOverlapExclusion`, `EmployeeServiceOverrides`, `BookingPriceSnapshot` — only on request).
2. Frontend: wire EmployeeService overrides + booking `price` snapshot (see `Booking-MVP-frontend.md`).
3. Later booking phases: customer self-cancel / reschedule — only when specified.
