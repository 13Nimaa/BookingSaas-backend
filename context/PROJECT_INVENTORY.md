# BookingSaaS Backend — Project Inventory

> Snapshot of what already exists in this repo (ASP.NET Core / .NET 10, PostgreSQL, Identity + JWT, Cloudinary).  
> Use this to resume work and see what still needs implementing.

**Stack:** `.NET 10` · `EF Core 10` · `Npgsql` · `ASP.NET Identity` · `JWT Bearer` · `Cloudinary` · CORS for `http://localhost:3000`

**Architecture layers:**
- `Controllers/` — HTTP API
- `Domain/Entities/` — entities + enums
- `Application/Dtos|Interfaces|Exceptions/` — contracts & DTOs
- `Infrastructure/Persistence|Services/` — EF, Cloudinary, token/booking/customer services
- `Migrations/` — 5 migrations applied historically

---

## 1. Auth model & roles

### How auth works
- Phone number is the username (`UserName = PhoneNumber`).
- Access token: JWT, **15 minutes**, claims: `NameIdentifier`, `MobilePhone`, `Role`(s), `jti`.
- Refresh token: random 64 bytes, stored **SHA-256 hashed** in `RefreshTokens`, also set as **HttpOnly cookie** `refreshToken` (Secure + SameSite=Strict, 7 days). Refresh rotates (old revoked, new issued).
- Default role on register: **`Customer`**.
- Becoming a business owner: `POST /api/Business/register` adds role **`BusinessOwner`**.
- Employees: owner adds by phone → user must already exist as `Customer` → role swapped to **`Employee`**.

### Roles in use (Identity roles, not the enum)
| Role | How assigned | Typical access |
|------|--------------|----------------|
| `Customer` | Auto on signup | Book (intended), public browse |
| `BusinessOwner` | Business register | Own business, employees, services, hours |
| `Employee` | Owner adds employee | Own working hours |
| `Admin` | Manual / not seeded in code | Create/delete business, CRM customers (as coded) |

### Dual / conflicting role concepts
- `ApplicationUser.Role` enum `{ Admin, Staff, Customer }` exists on the entity but is **not used** by controllers; real auth uses Identity roles (`Customer`, `BusinessOwner`, `Employee`, `Admin`).
- Enum says `Staff`; Identity uses `Employee` / `BusinessOwner`. Treat enum as legacy/unused until cleaned up.

### DI registered services (`Program.cs`)
| Interface | Implementation | Notes |
|-----------|----------------|-------|
| `ITokenService` | `TokenService` | Registered |
| `ICustomerService` | `CustomerService` | Registered |
| `IImageUploadService` | `CloudinaryImageUploadService` | Registered |
| `IBookingService` | `BookingService` | Registered (create, availability, status, list, mine, detail, expire) |
| (hosted) | `PendingBookingExpirationService` | `AddHostedService` — Pending→Expired poller |

---

## 2. Domain entities & data structures

### `ApplicationUser` (extends `IdentityUser`)
| Field | Type | Notes |
|-------|------|-------|
| Id | `string` | Identity PK |
| UserName | `string` | Set to phone |
| PhoneNumber | `string?` | Login key |
| Email | `string?` | Optional; editable via profile |
| FullName | `string` | Required custom field |
| Role | `UserRole` enum | Unused by API |
| ProfileImageUrl | `string?` | Cloudinary URL |
| ProfileImagePublicId | `string?` | Cloudinary public id |
| OwnedBusiness | `Business?` | 1:1 |
| EmployeeProfile | `Employee?` | 1:1 |

### `Business`
| Field | Type | Notes |
|-------|------|-------|
| Id | `int` | PK |
| Name | `string` | max 150 |
| City | `string` | max 100, indexed |
| Address | `string` | max 300 |
| TimeZoneId | `string` | required, max 100, default `Asia/Tehran` (migration `BookingDomainFoundation` not applied) |
| Description | `string?` | |
| ProfileImageUrl / ProfileImagePublicId | `string?` | |
| OwnerId | `string` | FK → user, **1 owner = 1 business** |
| Services / Employees | collections | |

**Constraint:** one business per owner (`WithOne(OwnedBusiness)`).

### `Employee`
| Field | Type | Notes |
|-------|------|-------|
| Id | `int` | |
| UserId | `string` | 1:1 with user |
| BusinessId | `int` | Cascade delete with business |
| Services | M2M via `EmployeeServices` | Managed via `api/employees/{id}/services` |
| WorkingHours / Appointments | collections | |

A user can only be one employee (unique 1:1 with user). Code also rejects if already an employee anywhere.

### `Service`
| Field | Type | Notes |
|-------|------|-------|
| Id | `int` | |
| Title | `string` | max 150 |
| Price | `decimal(10,2)` | Default price (source of truth when assignment has no override) |
| DurationMinutes | `int` | Default duration (source of truth when assignment has no override) |
| BusinessId | `int` | Cascade |
| Employees | M2M | Via explicit join entity `EmployeeService` / table `EmployeeServices` |

### `EmployeeService` (employee ↔ service assignment)
| Field | Type | Notes |
|-------|------|-------|
| EmployeesId | `int` | FK → Employee, part of composite PK |
| ServicesId | `int` | FK → Service, part of composite PK |
| PriceOverride | `decimal(10,2)?` | Null = inherit `Service.Price` |
| DurationMinutesOverride | `int?` | Null = inherit `Service.DurationMinutes` |

Composite PK `(EmployeesId, ServicesId)` prevents duplicates. Cascade delete with Employee/Service. Migration `EmployeeServiceOverrides` adds override columns (**not applied** outside Testcontainers).

### `WorkingHour`
| Field | Type | Notes |
|-------|------|-------|
| Id | `int` | |
| EmployeeId | `int` | Cascade |
| Date | `DateOnly` | Gregorian ISO `yyyy-MM-dd`; convert Jalali on frontend |
| DayOfWeek | `DayOfWeek` | Derived from `Date` on write |
| StartTime / EndTime | `TimeOnly` | Overlap checked per same `Date` in controller |

### `Customer` (per-business CRM + booking identity)
| Field | Type | Notes |
|-------|------|-------|
| Id | `int` | |
| UserId | `string?` | Optional; guest customers have null |
| Name | `string` | required, max 150 |
| PhoneNumber | `string?` | max 20 |
| Notes | `string?` | CRM notes |
| BusinessId | `int` | Cascade |
| Appointments | collection | |

**Indexes:** `(BusinessId)`, unique `(BusinessId, UserId)` where `UserId IS NOT NULL`.

`CustomerService` can:
- Link a registered user to a business customer (`GetOrCreateForUserAsync`)
- Create/find guest by phone (`GetOrCreateForGuestAsync`)
- Merge guest → user if same phone exists

### `Booking` (canonical booking model — Phase 3 create API live)
| Field | Type | Notes |
|-------|------|-------|
| Id | `int` | |
| BusinessId | `int` | Restrict delete (BookingConfiguration) |
| CustomerId | `int` | Restrict delete |
| ServiceId | `int` | Restrict delete |
| EmployeeId | `int` | Restrict delete |
| StartTime / EndTime | `DateTime` | UTC timestamps; EndTime calculated server-side |
| Notes | `string?` | |
| Status | `BookingStatus` | Pending / Confirmed / Cancelled / Completed / **Expired** |
| CreatorType | `BookingCreatorType` | Customer / Guest / BusinessOwner |
| CreatedByUserId | `string?` | optional FK → ApplicationUser; SetNull on user delete |
| CreatedAt | `DateTime` | default UtcNow |

**Table exists** (migration `AddBooking`). **`BookingConfiguration` + migration `BookingDomainFoundation`** (indexes on `(EmployeeId, StartTime)`, `(BusinessId, StartTime)`; creator fields; Restrict FKs) — **migration not applied** outside Testcontainers. **Create controller + DI wired** (`POST /api/bookings`, `POST /api/bookings/guest`).

### `Appointment` (older / parallel model — unused by API)
| Field | Type | Notes |
|-------|------|-------|
| Id | `int` | |
| ServiceId / EmployeeId / CustomerId | `int` | Restrict delete |
| StartTime / EndTime | `DateTime` | |
| Status | `AppointmentStatus` | same values as Booking |

Index on `(EmployeeId, StartTime)` for overlap checks — **never used by any endpoint**.  
**Decision needed:** keep `Booking` and drop `Appointment`, or unify them.

### `RefreshToken`
| Field | Type | Notes |
|-------|------|-------|
| Id | `int` | |
| Token | `string` | hashed |
| UserId | `string` | Cascade |
| CreatedAt / ExpiresAt | `DateTimeOffset` | |
| RevokedAt | `DateTimeOffset?` | |
| IsActive | computed | not revoked and not expired |

---

## 3. API endpoints (what they take / return)

Base: controllers under `api/...`. Auth header: `Authorization: Bearer <accessToken>` unless noted. Refresh cookie used for refresh/logout.

### 3.1 Auth — `api/Auth`

| Method | Path | Auth | Request body | Response |
|--------|------|------|--------------|----------|
| POST | `/register` | Public | `SignupDto` | `200` `AuthResponseDto` |
| POST | `/login` | Public | `LoginDto` | `200` `AuthResponseDto` |
| POST | `/refresh` | Cookie `refreshToken` | (none) | `200` `AuthResponseDto` |
| POST | `/logout` | Cookie optional | (none) | `{ message }` |
| GET | `/session` | Authorize | — | `{ Authenticated, User: UserDto, Roles }` |

**SignupDto**
```json
{ "name": "string", "phoneNumber": "string", "password": "string", "confirmPassword": "string" }
```

**LoginDto**
```json
{ "phoneNumber": "string", "password": "string" }
```

**AuthResponseDto**
```json
{
  "user": { "userId": "string", "name": "string", "phoneNumber": "string" },
  "accessToken": "string",
  "refreshToken": "string",
  "expiresAt": "datetimeoffset"
}
```
Also sets cookie `refreshToken`. Errors: `400` password mismatch / Identity errors, `409` phone taken, `401` bad login / bad refresh.

---

### 3.2 Profile — `api/Profile` (all Authorize)

| Method | Path | Request | Response |
|--------|------|---------|----------|
| GET | `/` | — | `UserProfileDto` |
| PUT | `/` | `UpdateProfileDto` | `UserProfileDto` |
| POST | `/image` | `multipart/form-data` field `file` | `{ message, profileImageUrl }` |
| DELETE | `/image` | — | `{ message }` |

**UserProfileDto**
```json
{
  "id": "string",
  "fullName": "string",
  "phoneNumber": "string|null",
  "profileImageUrl": "string|null",
  "email": "string|null",
  "roles": ["Customer"],
  "businessId": 1,
  "employeeId": null
}
```

**UpdateProfileDto**
```json
{ "fullName": "string", "email": "string|null" }
```
Images: JPG/PNG/WebP, max 5MB, folder `booking-saas/profiles`.

---

### 3.3 Business — `api/Business`

| Method | Path | Auth | Request | Response |
|--------|------|------|---------|----------|
| POST | `/` | `Admin` | `CreateBusinessDto` | `201` `BusinessDto` |
| GET | `/` | Public | — | `BusinessDto[]` |
| GET | `/{id}` | Public | — | `BusinessDto` |
| GET | `/mine` | Authorize | — | `MyBusinessResponse` |
| PUT | `/{id}` | `BusinessOwner` + must own | `CreateBusinessDto` (note: not `UpdateBusinessDto`) | `{ message, data: BusinessDto }` |
| DELETE | `/{id}` | `Admin` | — | `204` |
| POST | `/image` | Authorize (owner’s business) | form `file` | `{ message, profileImageUrl }` |
| DELETE | `/image` | Authorize | — | `{ message }` |
| POST | `/register` | Authorize | `CreateBusinessDto` | `AuthResponseDto` (new token with `BusinessOwner` role) |

**CreateBusinessDto / UpdateBusinessDto** (same shape; update DTO unused)
```json
{ "name": "string", "city": "string", "address": "string", "description": "string|null" }
```

**BusinessDto**
```json
{
  "id": 1,
  "name": "string",
  "city": "string",
  "address": "string",
  "description": "string|null",
  "profileImageUrl": "string|null",
  "ownerId": "string"
}
```

**MyBusinessResponse**
```json
{ "hasBusiness": true, "business": { /* BusinessDto or null */ } }
```

Business images folder: `booking-saas/businesses`.  
`/register` refuses if user already owns a business (`409`).

---

### 3.4 Employees — `api/business/{businessId}/employees`

All: **`BusinessOwner`** + must own that business.

| Method | Path | Request | Response |
|--------|------|---------|----------|
| GET | `/` | — | `EmployeeDto[]` |
| POST | `/` | `CreateEmployeeDto` | `201` `EmployeeDto` |
| DELETE | `/{employeeId}` | — | `{ message }` (Persian) |

**CreateEmployeeDto**
```json
{ "phoneNumber": "string" }
```
User must already exist and be in role `Customer`. Cannot already be an employee. Swaps `Customer` → `Employee` in a transaction.

**EmployeeDto**
```json
{
  "id": 1,
  "userId": "string",
  "fullName": "string",
  "phoneNumber": "string",
  "businessId": 1
}
```

Missing: get by id, update, re-assign, employee self-view, public list for booking UI.

---

### 3.5 Services — `api/business/{businessId}/services`

| Method | Path | Auth | Request | Response |
|--------|------|------|---------|----------|
| POST | `/` | `Admin` or `BusinessOwner` + must own | `CreateServiceDto` | `201` `ServiceDto` |
| GET | `/` | Public (`AllowAnonymous`) | — | `ServiceDto[]` |

**CreateServiceDto**
```json
{ "title": "string", "price": 0, "durationMinutes": 30 }
```

**ServiceDto**
```json
{
  "id": 1,
  "title": "string",
  "price": 0,
  "durationMinutes": 30,
  "businessId": 1
}
```

Missing: none for core CRUD (update/delete/get by id exist).

---

### 3.5b Employee ↔ Service — `api/employees/{employeeId}/services`

All: **`BusinessOwner`** + must own the employee's business (derived from DB; client does not send BusinessId).

| Method | Path | Request | Response |
|--------|------|---------|----------|
| GET | `/` | — | `ServiceDto[]` |
| POST | `/` | `AssignServiceDto` | `201` `ServiceDto` |
| DELETE | `/{serviceId}` | — | `{ message }` (Persian) |

**AssignServiceDto**
```json
{ "serviceId": 1 }
```

Rules: employee and service must share the same business; duplicates → `409`; remove blocked if any `Booking` exists for that employee+service pair → `409`; missing assignment → `404`. Join entity `EmployeeService` / table `EmployeeServices` has composite PK `(EmployeesId, ServicesId)` plus optional `PriceOverride` / `DurationMinutesOverride` (null = inherit Service defaults). PUT override endpoint not implemented yet.

---

### 3.6 Working hours — `api/business/{businessId}/employees/{employeeId}/working-hours`

Auth: **`Employee` or `BusinessOwner`**. Access if caller is that employee **or** owns the business.

| Method | Path | Request | Response |
|--------|------|---------|----------|
| POST | `/` | `CreateWorkingHourDto` | `201` `{ message, data: WorkingHourDto }` |
| GET | `/` | — | `WorkingHourDto[]` |
| PUT | `/{id}` | `CreateWorkingHourDto` | `{ message, data: WorkingHourDto }` |
| DELETE | `/{id}` | — | `{ message }` |

**CreateWorkingHourDto**
```json
{ "date": "2026-10-30", "startTime": "09:00:00", "endTime": "17:00:00" }
```
(`date`: Gregorian ISO; frontend converts from Jalali before send)

**WorkingHourDto**
```json
{
  "id": 1,
  "employeeId": 1,
  "date": "2026-10-30",
  "dayOfWeek": 5,
  "startTime": "09:00:00",
  "endTime": "17:00:00"
}
```
Validates end > start; rejects overlapping ranges on the same date (`409`).  
**Not public** — booking UI cannot currently read hours without auth.

---

### 3.7 Customers (CRM) — `api/business/{businessId}/customers`

Auth as coded: **`Admin`** only, **and** caller must be `business.OwnerId`.  
(So a normal `BusinessOwner` cannot hit these endpoints — likely a bug / unfinished.)

| Method | Path | Request | Response |
|--------|------|---------|----------|
| POST | `/` | `CreateCustomerDto` | `201` `CustomerDto` |
| GET | `/` | — | `CustomerDto[]` |
| GET | `/{customerId}` | — | `CustomerDto` |
| PUT | `/{customerId}` | `UpdateCustomerDto` | `CustomerDto` |

**CreateCustomerDto / UpdateCustomerDto**
```json
{ "name": "string", "phoneNumber": "string|null", "notes": "string|null" }
```

**CustomerDto**
```json
{
  "id": 1,
  "name": "string",
  "phoneNumber": "string|null",
  "notes": "string|null",
  "businessId": 1
}
```

Missing: delete, search, link to user, booking history for customer.

---

### 3.8 Bookings — `api/bookings`

| Method | Path | Auth | Request | Response |
|--------|------|------|---------|----------|
| GET | `/` | `BusinessOwner` or `Employee` | query: `status`, `employeeId`, `customerId`, `serviceId`, `fromDate`, `toDate`, `search`, `page`, `pageSize` | `200` `PagedResult<BookingListItemDto>` |
| GET | `/mine` | Authorize | query: `status`, `fromDate`, `toDate`, `page`, `pageSize` | `200` `PagedResult<CustomerBookingListItemDto>` |
| GET | `/{bookingId}` | `BusinessOwner` or `Employee` | — | `200` `BookingDetailsDto` |
| POST | `/` | Authorize | `CreateBookingDto` | `201` `BookingDto` |
| POST | `/guest` | Public | `CreateGuestBookingDto` | `201` `BookingDto` |
| GET | `/availability` | Public | query: `businessId`, `serviceId`, `employeeId`, `date` | `200` `AvailabilitySlotDto[]` |
| PATCH | `/{bookingId}/status` | `BusinessOwner` + must own booking's business | `UpdateBookingStatusDto` | `200` `BookingDto` |

**CreateBookingDto** (CustomerId/UserId not accepted; identity from JWT)
```json
{
  "businessId": 1,
  "serviceId": 1,
  "employeeId": 1,
  "startTime": "2026-01-01T10:00:00Z",
  "endTime": "2026-01-01T10:30:00Z"
}
```
`endTime` in the body is ignored; server sets `EndTime = StartTime + Service.DurationMinutes`.

**CreateGuestBookingDto**
```json
{
  "businessId": 1,
  "serviceId": 1,
  "employeeId": 1,
  "startTime": "...",
  "endTime": "...",
  "name": "string",
  "phoneNumber": "string"
}
```

**BookingDto**
```json
{
  "id": 1,
  "businessId": 1,
  "customerId": 1,
  "serviceId": 1,
  "employeeId": 1,
  "startTime": "...",
  "endTime": "...",
  "notes": null,
  "status": 0,
  "creatorType": 0,
  "createdByUserId": "string|null",
  "createdAt": "..."
}
```

**AvailabilitySlotDto**
```json
{ "startTime": "2026-11-15T09:00:00+03:30", "endTime": "2026-11-15T09:30:00+03:30" }
```
Timestamps are ISO 8601 `DateTimeOffset` in the business timezone offset. Slot step from `Booking:SlotIntervalMinutes` (default 15). Uses employee `WorkingHour` rows for the requested local date; occupying statuses = Pending + Confirmed; half-open intervals; past slots omitted. Does **not** reserve a slot.

**UpdateBookingStatusDto** (enums are numeric JSON — no `JsonStringEnumConverter`)
```json
{ "status": 1 }
```
`status`: `0` Pending, `1` Confirmed, `2` Cancelled, `3` Completed, `4` Expired. Allowed transitions only: Pending→Confirmed/Cancelled, Confirmed→Completed/Cancelled. Cancelled/Completed/Expired are terminal; Expired cannot be assigned. Errors: `400` invalid transition/enum, `403` not owner / wrong role, `404` missing booking.

**Booking list (`GET /api/bookings`)** — owner sees bookings for their owned business; employee sees only rows where `EmployeeId` matches their profile (client `employeeId` cannot broaden that scope). Dates are UTC ISO-8601 against `StartTime` (`fromDate`/`toDate` inclusive). `search` is case-insensitive on customer name/phone. Defaults: `page=1`, `pageSize=20` (max 100). Sort: `StartTime` desc, then `Id` desc.

**BookingListItemDto**
```json
{
  "id": 1,
  "startTime": "...",
  "endTime": "...",
  "status": 0,
  "creatorType": 0,
  "customerName": "string",
  "customerPhone": "string|null",
  "serviceName": "string",
  "serviceDurationMinutes": 30,
  "employeeName": "string",
  "businessId": 1
}
```

**PagedResult** (shared): `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`.

**Booking details (`GET /api/bookings/{bookingId}`)** — owner can read bookings for their owned business; employee can read only bookings assigned to their own `EmployeeId`. Out of scope → `403`; missing → `404` (`رزرو یافت نشد.`). Customer/anonymous rejected. Nested response; no Identity secrets / `createdByUserId`.

**BookingDetailsDto**
```json
{
  "id": 1,
  "businessId": 1,
  "startTime": "...",
  "endTime": "...",
  "status": 0,
  "creatorType": 0,
  "createdAt": "...",
  "notes": null,
  "customer": { "id": 1, "name": "string", "phoneNumber": "string|null" },
  "service": { "id": 1, "name": "string", "durationMinutes": 30, "price": 0 },
  "employee": { "id": 1, "name": "string", "profileImageUrl": "string|null" },
  "business": { "id": 1, "name": "string" }
}
```
`service.name` maps from `Service.Title` (entity has no Description). `employee.profileImageUrl` from `ApplicationUser.ProfileImageUrl`.

**Customer booking history (`GET /api/bookings/mine`)** — any authenticated user; returns only bookings whose `Customer.UserId` matches the JWT user id (never client-supplied CustomerId/UserId). Supports multiple Customer rows across businesses. Guest customers (`UserId` null) are never included via phone matching. No Customer record → empty `PagedResult`. Owner/employee tokens cannot broaden scope to other customers' history. Filters: `status`, `fromDate`/`toDate` (UTC inclusive on `StartTime`). Defaults: `page=1`, `pageSize=20` (max 100). Sort: `StartTime` desc, then `Id` desc.

**CustomerBookingListItemDto**
```json
{
  "id": 1,
  "startTime": "...",
  "endTime": "...",
  "status": 0,
  "creatorType": 0,
  "businessName": "string",
  "businessId": 1,
  "serviceName": "string",
  "serviceDurationMinutes": 30,
  "employeeName": "string"
}
```
Omits customer name/phone, notes, and Identity fields.

Rules (create): business/service/employee must exist and share business; employee must be assigned to service; new bookings are `Pending`; authenticated create → `CreatorType=Customer` + `CreatedByUserId`; guest → `CreatorType=Guest` + null user; guest phone matching a registered customer does **not** reuse that customer row. Overlap of Pending/Confirmed for the same employee is rejected (`409`, Persian) via Postgres exclusion constraint `EX_Bookings_Employee_Active_Time_Overlap` (half-open `[Start, End)`).

---

## 4. Migrations timeline

| Migration | Purpose |
|-----------|---------|
| `20260826_InitialCreate` | Identity + Business, Service, Employee, WorkingHour, Customer, Appointment |
| `20260829_AddRefreshToken` | Refresh tokens |
| `20260920_updateCustomerForCrm` | Customer CRM fields / business scoping |
| `20260924_AddBooking` | `Bookings` table |
| `20261015_AddProfileImageFields` | User + business Cloudinary image fields |
| `20261031_AddCustomerCrmFields` | Customer CRM email / IsActive / indexes |
| `20261031_BookingDomainFoundation` | Business.TimeZoneId; Booking creator fields; Restrict FKs + time indexes (**not applied**) |
| `20261101_BookingActiveOverlapExclusion` | `btree_gist` + partial EXCLUDE on active booking time ranges (**not applied**) |
| `20261102_EmployeeServiceOverrides` | `EmployeeServices.PriceOverride` + `DurationMinutesOverride` nullable (**not applied**) |

---

## 5. Config & secrets

`appsettings.json`:
- `ConnectionStrings:DefaultConnection` — Postgres `BookingSaaS`
- `Jwt:Issuer` / `Audience` — `BookingSaaS` (**`Jwt:Key` not in file**; expected via User Secrets)
- `Frontend:Origins` — `["http://localhost:3000"]`
- `Cloudinary` — CloudName / ApiKey / ApiSecret (empty in repo)
- `Booking:SlotIntervalMinutes` — default `15`
- `Booking:PendingExpirationMinutes` — default `30`
- `Booking:ExpirationPollingIntervalMinutes` — default `1`

Password policy: min length 6, non-alphanumeric not required.

---

## 6. What you HAVE (feature checklist)

| Feature | Status |
|---------|--------|
| Phone + password register/login | Done |
| JWT access + hashed refresh + cookie | Done |
| Session / logout / refresh rotation | Done |
| User profile get/update | Done |
| User profile image upload/delete (Cloudinary) | Done |
| Public business list + get by id | Done |
| My business | Done |
| Self-service business register → BusinessOwner | Done |
| Business update (owner) | Done |
| Business image upload/delete | Done |
| Admin create/delete business | Done |
| Employee add/list/remove (by phone) | Done |
| Service create + public list | Done |
| Working hours CRUD (employee/owner) | Done |
| CRM customer CRUD (partial; Admin-gated) | Partial |
| Guest/user customer get-or-create service | Done (service layer only) |
| Booking create (user + guest) | Done (`POST /api/bookings`, `/guest`) |
| Appointment entity | Schema only — unused |
| Employee ↔ Service assignment | Done (`EmployeeServicesController`) |
| Availability / free slots | Done (`GET /api/bookings/availability`) |
| Booking status transitions (confirm/cancel/complete) | Done (`PATCH /api/bookings/{id}/status`) |
| Pending booking auto-expiration | Done (`PendingBookingExpirationService`) |
| Booking list (owner/employee) | Done (`GET /api/bookings`) |
| Booking detail (owner/employee) | Done (`GET /api/bookings/{id}`) |
| Booking mine (customer history) | Done (`GET /api/bookings/mine`) |
| Payments / notifications / reviews | Missing |
| Admin seeding / role management API | Missing |
| Soft delete / audit | Missing |

---

## 7. What you still NEED to implement (priority-oriented)

### P0 — Core booking product (biggest gap)
1. ~~**Register `IBookingService` in DI** and add create `BookingController`.~~ Done (Phase 3).
2. **Remaining booking endpoints (suggested):**
   - ~~`GET /api/bookings/mine` — customer’s bookings~~ Done (Phase 10).
   - ~~Owner/employee list~~ Done (`GET /api/bookings` with filters/pagination).
   - ~~`PATCH /api/bookings/{id}/status` — Confirm / Cancel / Complete~~ Done (Phase 6).
   - Customer self-cancel / reschedule — still missing.
3. ~~Fill **`BookingDto`** response shape.~~ Done.
4. ~~**Availability API:** given business/employee/service/date → free slots from working hours − existing bookings (and service duration).~~ Done.
5. ~~**Overlap validation** on create~~ — Done (Phase 5 Postgres EXCLUDE; apply migration when ready).
6. **Decide: `Booking` vs `Appointment`** — pick one; migrate/remove the other to avoid dual sources of truth.

### P1 — Make owner dashboard usable
7. Fix **CustomerController** roles: allow `BusinessOwner` (and maybe `Employee`), not only `Admin`.
8. Service **Update / Delete / GetById**.
9. Public or booking-safe **employee list** + **working hours read** for customers.
10. ~~**Employee ↔ Service** assign/unassign endpoints~~ — Done.
11. Customer **Delete** + search by phone/name.
12. Use `UpdateBusinessDto` consistently (or delete unused DTO).

### P2 — Product polish
13. Business search/filter by city (index already on `City`).
14. Notes on booking create.
15. Seed roles (`Customer`, `BusinessOwner`, `Employee`, `Admin`) on startup.
16. Remove or sync unused `UserRole` enum on `ApplicationUser`.
17. Fix `CustomerService` naming: uses `user.UserName` as `Name` instead of `FullName`.
18. EmployeeDto missing namespace (works but inconsistent).
19. Password reset / OTP SMS (phone-centric auth will need this).
20. Email verification optional.

### P3 — Later SaaS features
21. Multi-branch / multi-business per owner (currently **hard 1:1**).
22. Payments, deposits, no-show fees.
23. Notifications (SMS/email/push) for booking events.
24. Reviews/ratings, public business pages extras.
25. Subscriptions / billing for the SaaS itself.
26. Analytics / reports.
27. Soft deletes, audit log, rate limiting.

---

## 8. Known inconsistencies / bugs to fix while resuming

| Issue | Detail |
|-------|--------|
| Customer CRM locked to `Admin` | Owners who own the business get `Forbid` unless they somehow have Admin role |
| Business `POST /` vs `/register` | Admin creates without role upgrade; register upgrades to BusinessOwner |
| `Update` business uses `CreateBusinessDto` | `UpdateBusinessDto` unused |
| Dual booking models | `Booking` + `Appointment` |
| `UserRole` enum vs Identity roles | Confusing / dead field |
| JWT Key absent from committed config | Must be in User Secrets locally |
| Cloudinary empty in repo | Must configure before image endpoints work |
| Refresh returns raw token in body **and** cookie | Frontend must pick one strategy |
| No public working-hours/employees for booking flow | Blocks public booking UI |

---

## 9. Suggested domain relationship (current target model)

```
ApplicationUser 1──1 Business (owner)
ApplicationUser 1──1 Employee
Business 1──* Service
Business 1──* Employee
Business 1──* Customer
Employee *──* Service  (EmployeeServices)
Employee 1──* WorkingHour
Customer + Service + Employee + Business → Booking (Pending→Confirmed→…)
```

`Appointment` is the legacy sibling of `Booking` and is not hooked to controllers.

---

## 10. File map (quick)

```
Controllers/
  AuthController.cs
  ProfileController.cs
  BusinessController.cs
  EmployeeController.cs
  EmployeeServicesController.cs
  ServiceController.cs
  WorkingHourController.cs
  CustomerController.cs
  BookingController.cs

Domain/Entities/
  ApplicationUser, Business, Employee, EmployeeService, Service, WorkingHour,
  Customer, Booking, Appointment, RefreshToken

Application/
  Dtos/{auth,Business,Employee,Service,WorkingHour,Customer,Profile,Booking}
  Interfaces/{IToken,ICustomer,IBooking,IImageUpload}
  Exceptions/ImageUploadException

Infrastructure/
  Persistence/AppDbContext + Configurations/*
  Services/{Token,Customer,Booking,CloudinaryImageUpload,PendingBookingExpiration}
```

---

*Generated from codebase inspection. Update this file as features land so the gap list stays accurate.*
