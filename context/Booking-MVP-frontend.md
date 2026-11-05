# Booking MVP — frontend implementation guide (specs 01–11)

Audience: Next.js frontend (`bookingsass-frontend`) consuming this ASP.NET Core API.

This document maps **feature-spec 01–11** to what the frontend should build. It reflects the **live** HTTP contracts (DTOs / controllers), not the backend implementation prompts.

Related docs (do not duplicate):

| Topic | Doc |
|-------|-----|
| Customer CRM (spec 01 detail) | `context/Customer-API-frontend.md` |
| Working hours (date-based) | `context/WorkingHour-API-frontend.md` |
| Business public list (search / city / sort / page) | `context/Business-List-API-frontend.md` |
| Backend phase status | `context/progress-tracker.md` |
| Known debt / deploy blockers | `context/current-issues.md` |

---

## 1. Conventions

| Topic | Rule |
|-------|------|
| Base URL | Your API host (dev CORS origin expected: `http://localhost:4000`) |
| Auth | `Authorization: Bearer <accessToken>`; send cookies if refresh uses HttpOnly cookie (`credentials: "include"`) |
| JSON | **camelCase** property names |
| Enums | Serialized as **numbers** by default (no `JsonStringEnumConverter` registered). Query/body may also accept enum **names** on bind — prefer **numbers** for consistency |
| Dates/times | Booking `startTime` / `endTime` / `createdAt` are ISO-8601 **UTC** `DateTime`. Availability slots are `DateTimeOffset` (business-local offset). Calendar `date` query is `yyyy-MM-dd` Gregorian |
| Jalali | Convert Jalali ↔ Gregorian **on the frontend** before/after API calls |
| Errors | FluentValidation → ProblemDetails-style `errors` object. Booking business errors often return a **plain Persian string** body (`400` / `404` / `409`) |
| Pagination | `PagedResult<T>`: `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage` |

### Enum reference

```ts
/** BookingStatus — numeric in JSON */
const BookingStatus = {
  Pending: 0,
  Confirmed: 1,
  Cancelled: 2,
  Completed: 3,
  Expired: 4,
} as const;

/** BookingCreatorType — numeric in JSON */
const BookingCreatorType = {
  Customer: 0,
  Guest: 1,
  BusinessOwner: 2, // reserved; owner-create-on-behalf not exposed yet
} as const;
```

### Shared types

```ts
type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
};

type BookingDto = {
  id: number;
  businessId: number;
  customerId: number;
  serviceId: number;
  employeeId: number;
  startTime: string; // ISO UTC
  endTime: string;   // ISO UTC (server-calculated from effective duration)
  notes: string | null;
  status: number;    // BookingStatus
  creatorType: number;
  createdByUserId: string | null;
  createdAt: string; // ISO UTC
  price: number | null; // snapshot at create; null on legacy rows
};
```

### Roles cheat sheet

| Role | Typical booking UI |
|------|--------------------|
| `Customer` | Book (auth), availability, `/mine` history |
| Anonymous | Availability, guest create |
| `BusinessOwner` | Employee↔service, CRM, list, detail, status |
| `Employee` | Own list + own detail only (no status change) |
| `Admin` | CRM (customers) with Admin rules; not the primary booking operator |

---

## 2. Spec → frontend map

| Spec | Backend theme | Frontend work? |
|------|---------------|----------------|
| **01** | Customer CRM | **Yes** — CRM screens (see dedicated doc) |
| **02** | Domain foundation (enums, `TimeZoneId`, EF) | **No HTTP** — FE only needs enum labels |
| **03** | Employee ↔ service assignment | **Yes** — owner setup UI |
| **04** | Create booking (auth + guest) | **Yes** — booking forms |
| **05** | Availability slots | **Yes** — slot picker |
| **06** | Overlap / concurrency | **UX only** — handle `409` on create |
| **07** | Owner status transitions | **Yes** — confirm / cancel / complete |
| **08** | Pending auto-expire (BackgroundService) | **UX only** — show Expired; pending countdown optional |
| **09** | Owner/employee list + filters | **Yes** — booking management table |
| **10** | Booking details | **Yes** — detail drawer/page |
| **11** | Customer `/mine` history | **Yes** — customer history |

Suggested **build order** for FE: 01 → 03 → working hours (existing) → 05 → 04 → 06 handling → 09 → 10 → 07 → 11 → polish 08 UX.

---

## 3. Spec 01 — Customer CRM

Full contract: **`context/Customer-API-frontend.md`**.

| Method | Path | Roles |
|--------|------|-------|
| `GET` | `/api/business/{businessId}/customers` | Admin, BusinessOwner |
| `GET` | `/api/business/{businessId}/customers/{customerId}` | Admin, BusinessOwner |
| `POST` | `/api/business/{businessId}/customers` | Admin, BusinessOwner |
| `PUT` | `/api/business/{businessId}/customers/{customerId}` | Admin, BusinessOwner |
| `DELETE` | `/api/business/{businessId}/customers/{customerId}` | Admin, BusinessOwner (soft: `isActive=false`) |

**FE checklist**

- Paged list (`items`, not bare array).
- Soft-delete + reactivate via `PUT` `isActive: true`.
- Never send `businessId` in body.
- Guests created via booking may appear as CRM customers with `userId` null — treat as normal CRM rows.

---

## 4. Spec 02 — Domain foundation (no booking UI)

No new endpoints. FE implications:

- Use `BookingStatus` / `BookingCreatorType` labels above.
- `Business.TimeZoneId` exists server-side (default `Asia/Tehran`) but is **not** currently on `BusinessDto` — do not invent a timezone editor unless a later API exposes it. Availability already interprets `date` in the business timezone.

---

## 5. Spec 03 — Employee ↔ service assignment (+ price/duration overrides)

Base: `/api/employees/{employeeId}/services`  
Auth: **BusinessOwner** only (must own the employee’s business).

### Types

```ts
type EmployeeServiceDto = {
  id: number; // service id
  title: string;
  defaultPrice: number;              // live Service.Price
  defaultDurationMinutes: number;    // live Service.DurationMinutes
  priceOverride: number | null;      // null = inherit default
  durationMinutesOverride: number | null;
  effectivePrice: number;            // priceOverride ?? defaultPrice
  effectiveDurationMinutes: number;  // durationMinutesOverride ?? defaultDurationMinutes
  businessId: number;
};

type AssignServiceDto = {
  serviceId: number;
  priceOverride?: number | null;           // omit/null → inherit
  durationMinutesOverride?: number | null; // omit/null → inherit
};

type UpdateEmployeeServiceDto = {
  priceOverride?: number | null;
  durationMinutesOverride?: number | null;
};
```

### Endpoints

#### List assigned services

`GET /api/employees/{employeeId}/services` → `200` `EmployeeServiceDto[]`

#### Assign

`POST /api/employees/{employeeId}/services`

```json
{ "serviceId": 5 }
```

Optional overrides:

```json
{ "serviceId": 5, "priceOverride": 300000, "durationMinutesOverride": 45 }
```

`201` → `EmployeeServiceDto`  
`400` invalid price/duration  
`404` employee/service missing  
`409` wrong business / already assigned  

`{ "serviceId": 5 }` alone remains valid (both overrides null).

#### Update overrides

`PUT /api/employees/{employeeId}/services/{serviceId}`

```json
{ "priceOverride": 300000, "durationMinutesOverride": 45 }
```

**Omit vs null (important):**

| JSON | Effect |
|------|--------|
| Property omitted | Leave that override unchanged |
| `"priceOverride": null` | Reset price to inherit Service default |
| `"durationMinutesOverride": 45` | Set only duration; leave price as-is if omitted |

`200` → `EmployeeServiceDto`  
`400` invalid values  
`404` assignment missing  
`403` not owner  

#### Remove

`DELETE /api/employees/{employeeId}/services/{serviceId}`

`200` → `{ "message": "تخصیص خدمت با موفقیت حذف شد." }`  
`404` assignment missing  
`409` has related bookings (cannot remove) / wrong business  

**FE checklist**

- Only show assign UI to owners.
- Show **effective** price/duration in owner UI; keep defaults visible for comparison.
- For PUT, send explicit `null` only when resetting; omit fields you are not editing.
- Booking create requires employee assigned to service — gate the employee dropdown by this list (or refetch after assign).
- On `409` when removing, explain that bookings exist.

---

## 6. Spec 04 — Booking creation

### Authenticated customer

`POST /api/bookings`  
Auth: any authenticated user (`[Authorize]`)  
Do **not** send `customerId` / `userId` — server resolves customer from JWT.

```ts
type CreateBookingDto = {
  businessId: number;
  serviceId: number;
  employeeId: number;
  startTime: string; // ISO UTC
  endTime: string;   // required by JSON shape; **ignored** by server
};
```

```json
{
  "businessId": 12,
  "serviceId": 5,
  "employeeId": 3,
  "startTime": "2026-11-10T06:30:00Z",
  "endTime": "2026-11-10T07:00:00Z"
}
```

**201** → `BookingDto` (`status: 0` Pending, `creatorType: 0` Customer)

### Guest

`POST /api/bookings/guest`  
Auth: **none** (`AllowAnonymous`)

```ts
type CreateGuestBookingDto = CreateBookingDto & {
  name: string;
  phoneNumber: string;
};
```

**201** → `BookingDto` (`creatorType: 1` Guest, `createdByUserId: null`)

Guest bookings are **not** linked to a registered account by phone match.

### Important create rules (FE)

1. Prefer picking `startTime` from **availability** slots (spec 05) — slot length already uses the employee’s effective duration.
2. Set `endTime` to any valid ISO (often `startTime + effectiveDuration`) — server **recalculates** from `DurationMinutesOverride ?? Service.DurationMinutes` and ignores client `endTime`.
3. Server stores `price` snapshot from `PriceOverride ?? Service.Price`. Do not send a client price.
4. On success, show Pending state and the returned `price` / `endTime`; do not claim the slot is “confirmed”.
5. Handle:

| Status | Meaning |
|--------|---------|
| `400` | Validation (ProblemDetails) |
| `404` | Business/service/employee missing (Persian string) |
| `409` | Overlap / missing assignment / wrong business (Persian string) — see spec 06 |
| `401` | Auth create without token |

---

## 7. Spec 05 — Availability

`GET /api/bookings/availability`  
Auth: **anonymous OK**

| Query | Type | Notes |
|-------|------|--------|
| `businessId` | int | required |
| `serviceId` | int | required |
| `employeeId` | int | required |
| `date` | `yyyy-MM-dd` | Gregorian calendar day in **business local** timezone |

```http
GET /api/bookings/availability?businessId=12&serviceId=5&employeeId=3&date=2026-11-10
```

```ts
type AvailabilitySlot = {
  startTime: string; // DateTimeOffset ISO, e.g. "2026-11-10T10:00:00+03:30"
  endTime: string;
};
```

**200** → `AvailabilitySlot[]`  
Empty array = no free slots (not an error).

**Duration rule**

Slot length = `EmployeeService.DurationMinutesOverride ?? Service.DurationMinutes` for the selected employee+service. Interval step stays **15 minutes** by default (`Booking:SlotIntervalMinutes`) even when duration is longer.

**FE checklist**

- Jalali date picker → Gregorian `yyyy-MM-dd` for `date`.
- Display slot labels in business-local time (offset is in the string).
- When user picks a slot, send that `startTime` (as UTC ISO) to create; use the slot’s `endTime` only for display — create recalculates.
- **Availability does not reserve** — always ready for create `409`.
- Occupy rules: Pending + Confirmed block; Cancelled / Expired / Completed do not. Overlaps use stored booking `StartTime`/`EndTime`.
- Employee must be assigned to the service (`409` otherwise).
- Past slots are omitted server-side.

---

## 8. Spec 06 — Concurrency (frontend handling)

No dedicated endpoint. Create enforces non-overlap for the same employee with half-open intervals `[start, end)` for Pending/Confirmed.

**FE**

- On create `409`, show the Persian message; refresh availability and ask the user to pick another slot.
- Adjacent slots are allowed (end of A == start of B).
- Do not spin-retry blindly.

---

## 9. Spec 07 — Owner status management

`PATCH /api/bookings/{bookingId}/status`  
Auth: **BusinessOwner** only

```json
{ "status": 1 }
```

```ts
type UpdateBookingStatusDto = { status: number };
```

**Allowed transitions**

| From | To |
|------|-----|
| Pending (0) | Confirmed (1) |
| Pending (0) | Cancelled (2) |
| Confirmed (1) | Completed (3) |
| Confirmed (1) | Cancelled (2) |

Rejected: any other transition; setting Expired manually; mutating Cancelled / Completed / Expired.

**200** → `BookingDto`  
**400** invalid transition (Persian string)  
**403** not owner / wrong business  
**404** missing  

**FE checklist**

- Owner booking detail/list actions: Confirm, Cancel, Complete — enable only for legal transitions.
- Employees/customers: hide status controls.
- After success, update local row or refetch list/detail.

---

## 10. Spec 08 — Pending auto-expiration (UX only)

Backend hosted service expires Pending when `CreatedAt <= UtcNow - PendingExpirationMinutes` (default **30**).

**FE**

- Treat `status === 4` (Expired) as terminal; no owner actions.
- Optional: countdown for Pending based on `createdAt + 30m` (client clock drift possible).
- After expire, slot becomes free again — refresh availability if user is booking.
- No FE API to trigger expiration.

---

## 11. Spec 09 — Booking list (owner / employee)

`GET /api/bookings`  
Auth: **BusinessOwner**, **Employee**

| Query | Default | Notes |
|-------|---------|--------|
| `status` | — | numeric enum |
| `employeeId` | — | owner filter; employee cannot escape own scope |
| `customerId` | — | |
| `serviceId` | — | |
| `fromDate` | — | inclusive lower on `StartTime` (UTC ISO) |
| `toDate` | — | inclusive upper on `StartTime` |
| `search` | — | customer name / phone (case-insensitive) |
| `page` | 1 | ≥ 1 |
| `pageSize` | 20 | 1…100 |

```ts
type BookingListItem = {
  id: number;
  startTime: string;
  endTime: string;
  status: number;
  creatorType: number;
  customerName: string;
  customerPhone: string | null;
  serviceName: string;
  serviceDurationMinutes: number;
  employeeName: string;
  businessId: number;
};
```

**200** → `PagedResult<BookingListItem>`  
Sort: `startTime` desc, then `id` desc.

**FE checklist**

- Owner: business-scoped management table + filters + pagination.
- Employee: same endpoint; UI should not imply cross-employee access.
- Customers/anonymous: do not call this API.
- Reset `page` to 1 when filters change; clamp `pageSize` ≤ 100.

---

## 12. Spec 10 — Booking details

`GET /api/bookings/{bookingId}`  
Auth: **BusinessOwner**, **Employee** (scoped)

```ts
type BookingDetails = {
  id: number;
  businessId: number;
  startTime: string;
  endTime: string;
  status: number;
  creatorType: number;
  createdAt: string;
  notes: string | null;
  customer: { id: number; name: string; phoneNumber: string | null };
  service: { id: number; name: string; durationMinutes: number; price: number | null }; // price = Booking.Price snapshot
  employee: { id: number; name: string; profileImageUrl: string | null };
  business: { id: number; name: string };
};
```

**200** → `BookingDetails`  
**403** / **404** outside scope or missing  

Does **not** expose `createdByUserId` / Identity internals.

**FE checklist**

- Owner/employee detail drawer from list row.
- Wire status actions (spec 07) for owners.
- Customers use `/mine` + create response — not this endpoint.

---

## 13. Spec 11 — Customer booking history

`GET /api/bookings/mine`  
Auth: any authenticated user (scoped by `Customer.UserId` from JWT)

| Query | Default | Notes |
|-------|---------|--------|
| `status` | — | |
| `fromDate` / `toDate` | — | on `StartTime` |
| `page` | 1 | |
| `pageSize` | 20 | max 100 |

```ts
type CustomerBookingListItem = {
  id: number;
  startTime: string;
  endTime: string;
  status: number;
  creatorType: number;
  businessName: string;
  businessId: number;
  serviceName: string;
  serviceDurationMinutes: number;
  employeeName: string;
};
```

**200** → `PagedResult<CustomerBookingListItem>`  
Empty `items` if user has no Customer rows.  
Guest bookings are **not** merged by phone. Multi-business Customer rows are supported (history can span businesses).

**FE checklist**

- Customer “My bookings” page.
- Do not use owner list/detail APIs for customers.
- Owners/employees should not use `/mine` to inspect other people (server scopes to their own Customer links only).

---

## 14. Suggested screens

### Public / customer

1. Business browse (existing Business API).
2. **Book flow:** pick service → employee (must be assigned) → Jalali date → **availability** → confirm → `POST /api/bookings` or guest form → success Pending.
3. **My bookings:** `/mine` + status badges (Pending / Confirmed / …).

### Owner

1. **Setup:** services, employees, **assign services** (03), working hours (see WorkingHour doc), CRM (01).
2. **Bookings board:** list (09) + filters + detail (10) + status (07).
3. Show creator badge: Customer vs Guest.

### Employee

1. Own bookings list + detail (read-only status).

---

## 15. Status badge copy (suggested Persian)

| Value | Label |
|------:|-------|
| 0 | در انتظار تایید |
| 1 | تایید شده |
| 2 | لغو شده |
| 3 | انجام شده |
| 4 | منقضی شده |

Creator:

| Value | Label |
|------:|-------|
| 0 | مشتری |
| 1 | مهمان |
| 2 | صاحب کسب‌وکار |

---

## 16. End-to-end smoke checklist (manual)

1. Owner assigns service to employee (03).
2. Owner sets working hours for a Gregorian date.
3. Anonymous: availability for that date returns slots (05).
4. Guest creates booking for a slot (04) → Pending.
5. Second overlapping create → `409` (06).
6. Owner lists booking (09), opens detail (10), confirms (07).
7. Authenticated customer creates booking → appears in `/mine` (11).
8. Leave a Pending booking > 30 minutes → becomes Expired (08); slot free again.

---

## 17. Out of scope for MVP FE (do not invent)

- Customer self-cancel / reschedule  
- Owner create-on-behalf (`BusinessOwner` creator type unused in create APIs)  
- Guest lookup by phone  
- Notifications / payments  
- Editing `Business.TimeZoneId` (not on Business DTO yet)  
- Trusting client `customerId` on create  

---

## 18. Quick API index

| Method | Path | Auth |
|--------|------|------|
| CRUD | `/api/business/{businessId}/customers…` | Owner/Admin |
| `GET/POST/PUT/DELETE` | `/api/employees/{employeeId}/services…` | Owner |
| `GET` | `/api/bookings/availability` | Anonymous |
| `POST` | `/api/bookings` | Authenticated |
| `POST` | `/api/bookings/guest` | Anonymous |
| `GET` | `/api/bookings` | Owner/Employee |
| `GET` | `/api/bookings/mine` | Authenticated |
| `GET` | `/api/bookings/{bookingId}` | Owner/Employee |
| `PATCH` | `/api/bookings/{bookingId}/status` | Owner |

Working hours (prerequisite for availability): `context/WorkingHour-API-frontend.md`.
