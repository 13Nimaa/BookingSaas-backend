# Business dashboard API — frontend guide

Owner-only dashboard aggregation for the Business Owner home screen.

**Status:** live on backend (`GET /api/Business/dashboard`)  
**JSON:** camelCase  
**Auth:** `BusinessOwner` role only — business is resolved from the JWT owner, **never** send `businessId`

Related:

| Topic | Doc |
|-------|-----|
| Booking statuses / list | `context/Booking-MVP-frontend.md` |
| Customers | `context/Customer-API-frontend.md` |
| Spec | `context/feature spec/18-dashboard-api.md` |

---

## What to know

| Fact | Detail |
|------|--------|
| One call | Single response powers summary cards, weekly chart, today’s list, top services, capacity |
| Timezone | All “today” / “this week” / “this month” use `Business.TimeZoneId` (default `Asia/Tehran`) |
| Week | Saturday → Friday (Iran business week), always **7** points in order |
| Money | `todayEstimatedRevenue` and chart values are **estimated booking value** from `Booking.price` snapshots — **not** settled payments |
| No trends | API does **not** return “+3 vs yesterday” / “+8% vs last month” — compute locally or omit |
| No monthly income card | Use `summary.todayEstimatedRevenue` (today) + `weeklyRevenue` (week). There is no monthly total field |
| Capacity | Present when calculable from employee working hours; `utilizationPercent` is `null` when no available minutes |

---

## Endpoint

```http
GET /api/Business/dashboard
Authorization: Bearer <accessToken>
```

Routing is case-insensitive (`/api/business/dashboard` also works).

| Status | When |
|--------|------|
| `200` | Dashboard payload |
| `400` | Invalid business timezone (Persian message body) |
| `401` | Missing/invalid token |
| `403` | Authenticated but not `BusinessOwner` |
| `404` | Owner has no business (`کسب‌وکار پیدا نشد.`) |

No query params. No request body.

---

## Types

```ts
/** BookingStatus — numeric in JSON (same as booking APIs) */
const BookingStatus = {
  Pending: 0,
  Confirmed: 1,
  Cancelled: 2,
  Completed: 3,
  Expired: 4,
} as const;

type BusinessDashboardDto = {
  summary: DashboardSummaryDto;
  capacity: DashboardCapacityDto | null;
  weeklyRevenue: WeeklyRevenueDayDto[]; // length 7
  todayBookings: DashboardTodayBookingsDto;
  topServices: DashboardTopServicesDto;
};

type DashboardSummaryDto = {
  todayBookingsCount: number;
  /** Sum of completed-today Booking.price; 0 if none completed; null if completed exist but all prices null */
  todayEstimatedRevenue: number | null;
  customerCount: number;
};

type DashboardCapacityDto = {
  occupiedMinutes: number;
  availableMinutes: number;
  /** null when availableMinutes === 0 */
  utilizationPercent: number | null;
};

type WeeklyRevenueDayDto = {
  date: string; // "yyyy-MM-dd"
  dayOfWeek: number; // 0=Sunday … 6=Saturday
  weekdayKey: string; // "saturday" | "sunday" | …
  estimatedRevenue: number; // decimal; 0 if no priced completed bookings that day
  completedBookingCount: number;
};

type DashboardTodayBookingsDto = {
  totalCount: number; // all operational bookings today (not just items.length)
  items: DashboardBookingItemDto[]; // max 5, StartTime asc
};

type DashboardBookingItemDto = {
  id: number;
  customerName: string | null;
  serviceTitle: string;
  employeeName: string;
  startTime: string; // ISO UTC
  endTime: string;   // ISO UTC
  status: number;    // BookingStatus — Pending | Confirmed | Completed only
  price: number | null;
};

type DashboardTopServicesDto = {
  periodStart: string; // "yyyy-MM-dd" — business-local month start
  periodEnd: string;   // "yyyy-MM-dd" — inclusive month end
  items: DashboardTopServiceItemDto[]; // max 5, by booking count desc
};

type DashboardTopServiceItemDto = {
  serviceId: number;
  title: string;
  bookingCount: number;
  percentage: number; // share of returned groups; 0–100
};
```

---

## Example `200`

```json
{
  "summary": {
    "todayBookingsCount": 18,
    "todayEstimatedRevenue": 2450000,
    "customerCount": 324
  },
  "capacity": {
    "occupiedMinutes": 240,
    "availableMinutes": 480,
    "utilizationPercent": 50.0
  },
  "weeklyRevenue": [
    {
      "date": "2026-11-07",
      "dayOfWeek": 6,
      "weekdayKey": "saturday",
      "estimatedRevenue": 0,
      "completedBookingCount": 0
    },
    {
      "date": "2026-11-08",
      "dayOfWeek": 0,
      "weekdayKey": "sunday",
      "estimatedRevenue": 850000,
      "completedBookingCount": 3
    }
  ],
  "todayBookings": {
    "totalCount": 18,
    "items": [
      {
        "id": 101,
        "customerName": "امیر رضایی",
        "serviceTitle": "اصلاح مو + ریش",
        "employeeName": "علی",
        "startTime": "2026-11-07T07:00:00Z",
        "endTime": "2026-11-07T07:45:00Z",
        "status": 1,
        "price": 450000
      }
    ]
  },
  "topServices": {
    "periodStart": "2026-11-01",
    "periodEnd": "2026-11-30",
    "items": [
      {
        "serviceId": 3,
        "title": "اصلاح مو",
        "bookingCount": 42,
        "percentage": 56.0
      }
    ]
  }
}
```

(`weeklyRevenue` always has 7 entries; truncated above.)

---

## UI mapping (owner dashboard)

| UI widget | API field | Notes |
|-----------|-----------|--------|
| نوبت‌های امروز (count card) | `summary.todayBookingsCount` | Excludes Cancelled + Expired |
| درآمد / ارزش امروز | `summary.todayEstimatedRevenue` | Label as estimated booking value, not “paid” |
| مشتریان | `summary.customerCount` | All `Customer` rows for the business (incl. guests with a Customer record) |
| ظرفیت / بهره‌وری | `capacity.utilizationPercent` | Hide or show “—” when `capacity` is null or `utilizationPercent` is null |
| نمودار درآمد هفتگی | `weeklyRevenue` | X = `weekdayKey` / Persian weekday from `dayOfWeek`; Y = `estimatedRevenue` |
| لیست نوبت‌های امروز | `todayBookings.items` | Use `totalCount` for “مشاهده همه”; link to bookings list |
| خدمات پرفروش | `topServices.items` | Period is current business-local **month** (`periodStart`–`periodEnd`) |

### Do not expect from this API

- Growth / delta vs yesterday or last month  
- Confirmed payment / transaction totals  
- Monthly income total as a single summary field  
- Full booking list (widget is capped at **5**; use `GET /api/bookings` for the rest)

---

## Weekly chart

1. Fetch dashboard once on mount (and on focus/refetch if you want live-ish data).
2. Use `weeklyRevenue` in array order — do not re-sort by weekday name.
3. Map labels with Persian weekdays, e.g.:

```ts
const weekdayFa: Record<string, string> = {
  saturday: "شنبه",
  sunday: "یکشنبه",
  monday: "دوشنبه",
  tuesday: "سه‌شنبه",
  wednesday: "چهارشنبه",
  thursday: "پنجشنبه",
  friday: "جمعه",
};
```

4. Chart series: `estimatedRevenue` (number). Empty days are `0`, not omitted.
5. Optional footer: sum of the 7 `estimatedRevenue` values; “highest day” = max by revenue then by date.
6. Copy should say estimated / booking value, not “پرداخت شده”.

---

## Today’s bookings widget

| Rule | Detail |
|------|--------|
| Statuses included | Pending (`0`), Confirmed (`1`), Completed (`3`) |
| Excluded | Cancelled (`2`), Expired (`4`) |
| Order | `startTime` ascending |
| Cap | Up to **5** in `items` |
| Guest names | Come from linked Customer name when present |
| Times | Convert `startTime` / `endTime` from UTC to business display timezone (or browser local — prefer business TZ if you already have it from `/Business/mine`) |
| “View all” | Navigate to bookings list; `totalCount` is the badge/source of truth |

Suggested status badges (same as booking MVP):

| status | Label |
|--------|--------|
| 0 | در انتظار |
| 1 | تأیید شده |
| 3 | انجام‌شده |

---

## Capacity card

```ts
function formatCapacity(c: DashboardCapacityDto | null): string {
  if (!c || c.utilizationPercent == null) return "—";
  return `${c.utilizationPercent}%`;
}
```

- Built from today’s employee working hours ∩ operational bookings.  
- Employees with no assigned services do not add capacity.  
- If no hours scheduled today → `availableMinutes: 0`, `utilizationPercent: null`.

---

## Top services

- Window: **current calendar month** in the business timezone (`topServices.periodStart` / `periodEnd`).
- Metric: completed bookings only; percentages are within the returned top group (not all services ever).
- Empty business / no completed this month → `items: []`.

---

## Fetch sketch (Next.js)

```ts
async function fetchDashboard(accessToken: string): Promise<BusinessDashboardDto> {
  const res = await fetch(`${API_BASE}/api/Business/dashboard`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    credentials: "include",
    cache: "no-store",
  });

  if (res.status === 401) throw new Error("unauthorized");
  if (res.status === 403) throw new Error("forbidden");
  if (res.status === 404) throw new Error("no-business");
  if (!res.ok) throw new Error(await res.text());

  return res.json();
}
```

Wire only on owner dashboard routes. Customers / employees will get `403`.

---

## Empty business

Valid `200` with zeros / empty lists — do not treat as error:

- `todayBookingsCount: 0`
- `todayEstimatedRevenue: 0`
- `customerCount: 0`
- `weeklyRevenue`: 7 zero days
- `todayBookings.items: []`, `totalCount: 0`
- `topServices.items: []`
- `capacity`: object with `0` / `null` percent, or still present

---

## Checklist

- [ ] Call only as `BusinessOwner` with Bearer token  
- [ ] No `businessId` in query/body  
- [ ] Chart uses all 7 `weeklyRevenue` points in order  
- [ ] Money labeled as estimated booking value  
- [ ] Today list uses `items` + “view all” via `totalCount` / bookings page  
- [ ] Status badges use numeric `BookingStatus`  
- [ ] Handle `todayEstimatedRevenue === null` and `capacity.utilizationPercent === null`  
- [ ] Handle `404` when owner has not created a business yet  
