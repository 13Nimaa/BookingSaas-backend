# Business detail API — frontend guide

Public business profile page: business info + staff + eligible services (effective pricing) + upcoming working hours.

**Endpoint:** `GET /api/Business/{id}`  
**Auth:** none (public)  
**JSON:** camelCase  
**Status:** live on backend — implement against this contract

> Breaking change vs older clients: response is now `BusinessDetailsDto` (includes `employees[]`), not a bare `BusinessDto`. List endpoint (`GET /api/Business`) is unchanged and still returns `PagedResult<BusinessDto>` without nested staff.

Related:

| Topic | Doc |
|-------|-----|
| Public list (search / city / page) | `context/Business-List-API-frontend.md` |
| Business gallery upload / delete | `context/Business-Gallery-API-frontend.md` |
| Availability / create booking | `context/Booking-MVP-frontend.md` |
| Owner working-hours manage | `context/WorkingHour-API-frontend.md` |

---

## Backend gaps closed (use these on the public page)

These used to be owner-only; they are now nested on this public detail response (no phone / userId).

| Need | Was | Now (on `GET /api/Business/{id}`) |
|------|-----|-----------------------------------|
| Public employee list | `GET /api/business/{id}/employees` (owner-only, includes phone) | `employees[]` → `id`, `fullName`, `avatarUrl` |
| Public employee↔service (eligible staff + effective pricing) | `GET /api/employees/{id}/services` (owner-only) | `employees[].services[]` → `id`, `title`, `effectivePrice`, `effectiveDurationMinutes` |
| Public working-hours read (optional hours section) | working-hours GET (owner/employee-only) | `employees[].workingHours[]` (+ optional `hoursFrom` / `hoursTo`) |

---

## What this unlocks on the FE

| UI need | Field / source |
|---------|----------------|
| Business header | `name`, `city`, `address`, `description`, `profileImageUrl`, `businessGallery` |
| Staff list / picker | `employees[]` → `id`, `fullName`, `avatarUrl` |
| Who can do a service + price | `employee.services[]` → `id`, `title`, `effectivePrice`, `effectiveDurationMinutes` |
| Optional “hours” section | `employee.workingHours[]` |
| Actual free slots | still `GET /api/bookings/availability` (not this endpoint) |

**Do not** call these from the public page (owner-only / may leak phone):

- `GET /api/business/{id}/employees`
- `GET /api/employees/{id}/services`
- `GET /api/business/{id}/employees/{employeeId}/working-hours`

Catalog services without staff context (optional): `GET /api/business/{id}/services` remains public.

---

## Query params

| Param | Type | Default | Notes |
|-------|------|---------|--------|
| `hoursFrom` | string (`yyyy-MM-dd`) | business-local **today** | Inclusive Gregorian start for nested hours |
| `hoursTo` | string (`yyyy-MM-dd`) | `hoursFrom + 30 days` | Inclusive; total span ≤ **90** days |

Convert Jalali → Gregorian **before** sending. Invalid / inverted / oversized window → `400`.

```http
GET /api/Business/12
GET /api/Business/12?hoursFrom=2026-11-05&hoursTo=2026-11-19
```

---

## Types

```ts
type PublicEmployeeServiceDto = {
  id: number; // service id
  title: string;
  effectivePrice: number;
  effectiveDurationMinutes: number;
};

type WorkingHourDto = {
  id: number;
  employeeId: number;
  date: string; // Gregorian yyyy-MM-dd
  dayOfWeek: number; // 0=Sunday … 6=Saturday
  startTime: string; // HH:mm:ss
  endTime: string;
};

type PublicEmployeeDto = {
  id: number;
  fullName: string;
  avatarUrl: string | null;
  services: PublicEmployeeServiceDto[];
  workingHours: WorkingHourDto[];
};

type BusinessGalleryImageDto = {
  url: string;
  publicId: string; // use with DELETE /api/Business/gallery?publicId=
};

type BusinessDetailsDto = {
  id: number;
  name: string;
  city: string;
  address: string;
  description: string | null;
  profileImageUrl: string | null;
  businessGallery: BusinessGalleryImageDto[]; // optional; empty array when none
  ownerId: string;
  employees: PublicEmployeeDto[];
};

type BusinessDetailQuery = {
  hoursFrom?: string; // yyyy-MM-dd
  hoursTo?: string;
};
```

Privacy: no `phoneNumber`, no `userId` on employees.

---

## Example response

```json
{
  "id": 12,
  "name": "Salon Tehran",
  "city": "Tehran",
  "address": "Valiasr St.",
  "description": "Hair & beauty",
  "profileImageUrl": "https://…/biz.jpg",
  "businessGallery": [
    { "url": "https://…/gallery1.jpg", "publicId": "booking-saas/businesses/gallery/…" }
  ],
  "ownerId": "abc-…",
  "employees": [
    {
      "id": 3,
      "fullName": "Ada Stylist",
      "avatarUrl": null,
      "services": [
        {
          "id": 7,
          "title": "Haircut",
          "effectivePrice": 150000,
          "effectiveDurationMinutes": 45
        }
      ],
      "workingHours": [
        {
          "id": 41,
          "employeeId": 3,
          "date": "2026-11-06",
          "dayOfWeek": 5,
          "startTime": "09:00:00",
          "endTime": "17:00:00"
        }
      ]
    }
  ]
}
```

---

## Owner gallery upload (optional)

Create / register / update stay JSON. After create (or on edit), upload gallery images:

| Method | Path | Auth | Body |
|--------|------|------|------|
| `POST` | `/api/Business/gallery` | owner | `multipart/form-data` field **`files`** (1–N) |
| `DELETE` | `/api/Business/gallery?publicId=` | owner | — |

Rules: JPG/PNG/WebP, ≤5 MB each, **max 10** images total. Response includes the full `businessGallery` array.

```ts
async function uploadGallery(accessToken: string, files: File[]) {
  const form = new FormData();
  for (const file of files) form.append("files", file);
  const res = await fetch(`${API_BASE}/api/Business/gallery`, {
    method: "POST",
    headers: { Authorization: `Bearer ${accessToken}` },
    body: form,
  });
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as {
    message: string;
    businessGallery: BusinessGalleryImageDto[];
  };
}
```

Public page: render `business.businessGallery.map(g => g.url)` (empty array = no gallery).

---

## Fetch helper

```ts
async function getBusinessDetail(
  businessId: number,
  query: BusinessDetailQuery = {},
): Promise<BusinessDetailsDto> {
  const params = new URLSearchParams();
  if (query.hoursFrom) params.set("hoursFrom", query.hoursFrom);
  if (query.hoursTo) params.set("hoursTo", query.hoursTo);

  const qs = params.toString();
  const res = await fetch(
    `${API_BASE}/api/Business/${businessId}${qs ? `?${qs}` : ""}`,
  );

  if (res.status === 404) throw new Error("Business not found");
  if (!res.ok) throw new Error(await res.text());

  return (await res.json()) as BusinessDetailsDto;
}
```

No `Authorization` header required.

---

## Suggested UI flow

### 1. Load detail page

```ts
const business = await getBusinessDetail(businessId);
// header from business.*
// staff from business.employees
```

404 → not-found UI.

### 2. Service picker → eligible staff

Prefer nested assignments (effective price/duration), not catalog-only defaults:

```ts
function staffForService(business: BusinessDetailsDto, serviceId: number) {
  return business.employees.filter((e) =>
    e.services.some((s) => s.id === serviceId),
  );
}

function assignment(
  employee: PublicEmployeeDto,
  serviceId: number,
): PublicEmployeeServiceDto | undefined {
  return employee.services.find((s) => s.id === serviceId);
}

// display:
// assignment(employee, serviceId)?.effectivePrice
// assignment(employee, serviceId)?.effectiveDurationMinutes
```

Build the service list from either:

- unique `employee.services` across all staff, or
- `GET /api/business/{id}/services` (catalog) then filter staff as above.

### 3. Optional hours section

Show `employee.workingHours` (or merge all employees). Convert `date` to Jalali for display.

Widen the window when the user pages the calendar:

```ts
await getBusinessDetail(businessId, {
  hoursFrom: "2026-11-01",
  hoursTo: "2026-11-30",
});
```

Keep span ≤ 90 days.

### 4. Book a slot

1. User picks service + employee (+ date).
2. Call `GET /api/bookings/availability` with those ids + date.
3. Create via `POST /api/bookings` (auth) or `POST /api/bookings/guest`.

Working hours on this detail response are **informational**. Availability is the source of truth for free slots.

Contracts for availability / create: `context/Booking-MVP-frontend.md`.

---

## Errors

| Status | When | FE action |
|--------|------|-----------|
| `200` | OK | Render detail |
| `400` | Bad `hoursFrom`/`hoursTo` (inverted or > 90 days) | Fix dates; show validation message |
| `404` | Unknown business id | Not-found page |

---

## FE checklist

1. Type detail as `BusinessDetailsDto` with `employees` (not list `BusinessDto`).
2. Never show / request phone or userId for public staff.
3. Price & duration from `effectivePrice` / `effectiveDurationMinutes` after staff+service selection.
4. Filter eligible staff with `employee.services`.
5. Jalali ↔ Gregorian on the client for `hoursFrom` / `hoursTo` and `workingHours[].date`.
6. Hours section optional; slot grid uses availability API.
7. Do not use owner employee / employee-service / working-hour GETs on the public page.
8. Empty `employees: []` is valid (business with no staff yet).

---

## Quick map: list → detail → book

```
GET /api/Business?…          → cards (BusinessDto)
GET /api/Business/{id}       → page (BusinessDetailsDto + staff/services/hours)
GET /api/bookings/availability → slots
POST /api/bookings[/guest]   → create
```
