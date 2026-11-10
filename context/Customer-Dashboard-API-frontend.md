# Customer dashboard API — frontend guide

Customer home screen: summary cards, upcoming widget, recent list, favorites, cancel.

**Status:** live on backend  
**JSON:** camelCase  
**Auth:** Bearer access token (any authenticated user)  
**Spec:** `context/feature spec/19-customer-dashboard-api.md`

**Not in this API:** rating/score, discount banners, notification badge.

---

## Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/customer/dashboard` | One-shot home payload |
| GET | `/api/customer/favorites` | Favorite salons list |
| POST | `/api/customer/favorites` | Add favorite `{ businessId }` |
| DELETE | `/api/customer/favorites/{businessId}` | Remove favorite |
| GET | `/api/bookings/{bookingId}` | Detail (own booking OK) |
| POST | `/api/bookings/{bookingId}/cancel` | Self-cancel |
| GET | `/api/bookings/mine` | Full history (“مشاهده همه”) |
| GET | `/api/Business?search=` | Hero search |
| GET | `/api/Profile` | Header name / avatar |

---

## Dashboard

```http
GET /api/customer/dashboard
Authorization: Bearer <accessToken>
```

| Status | When |
|--------|------|
| 200 | Payload |
| 401 | Missing/invalid token |

### Types

```ts
const BookingStatus = {
  Pending: 0,
  Confirmed: 1,
  Cancelled: 2,
  Completed: 3,
  Expired: 4,
} as const;

type CustomerDashboardDto = {
  summary: {
    upcomingBookingsCount: number;
    totalBookingsCount: number;
    favoriteSalonsCount: number;
  };
  upcomingBookings: {
    totalCount: number; // all upcoming, not just items.length
    items: CustomerDashboardBookingItemDto[]; // max 5, StartTime asc
  };
  recentBookings: CustomerDashboardBookingItemDto[]; // max 5, StartTime desc
};

type CustomerDashboardBookingItemDto = {
  id: number;
  startTime: string; // UTC ISO
  endTime: string;
  status: number;
  businessId: number;
  businessName: string;
  businessProfileImageUrl: string | null;
  serviceName: string;
  employeeName: string;
};
```

### UI mapping

| UI | Field |
|----|--------|
| رزروهای پیش‌رو card | `summary.upcomingBookingsCount` |
| کل رزروها card | `summary.totalBookingsCount` |
| سالن‌های مورد علاقه card | `summary.favoriteSalonsCount` |
| امتیاز شما | **omit** (not returned) |
| Upcoming list | `upcomingBookings.items` |
| Recent table | `recentBookings` |
| مشاهده همه | navigate to `/api/bookings/mine` |

**Upcoming rule:** `status` is Pending(0) or Confirmed(1) **and** `startTime >= now (UTC)`.

Convert times to Jalali on the client. Status badges:

| status | Label |
|--------|--------|
| 0 | در انتظار تأیید |
| 1 | تأیید شده |
| 2 | لغو شده |
| 3 | تکمیل شده |
| 4 | منقضی شده |

---

## Favorites

```http
POST /api/customer/favorites
Authorization: Bearer <accessToken>
Content-Type: application/json

{ "businessId": 1 }
```

```ts
type BusinessFavoriteDto = {
  businessId: number;
  name: string;
  city: string;
  profileImageUrl: string | null;
  favoritedAt: string;
};
```

- POST is **idempotent** if already favorited (200 + existing row).
- Missing business → `404` (`کسب‌وکار پیدا نشد.`)
- DELETE missing → `404` (`علاقه‌مندی یافت نشد.`)

---

## Cancel booking

```http
POST /api/bookings/{bookingId}/cancel
Authorization: Bearer <accessToken>
```

Returns `BookingDto` with `status: 2` (Cancelled).

| Status | When |
|--------|------|
| 200 | Cancelled |
| 400 | Not Pending/Confirmed (`لغو این رزرو مجاز نیست.`) |
| 403 | Not the booking’s customer |
| 404 | Missing |
| 401 | No token |

Show **لغو رزرو** only when status is `0` or `1` and start is still in the future (optional UX).

---

## Booking detail

```http
GET /api/bookings/{bookingId}
Authorization: Bearer <accessToken>
```

Customer may read **own** bookings (`Customer.UserId` match).  
`business` now includes `profileImageUrl` (nullable).

---

## Fetch example

```ts
async function loadCustomerHome(token: string) {
  const res = await fetch(`${API_BASE}/api/customer/dashboard`, {
    headers: { Authorization: `Bearer ${token}` },
    credentials: "include",
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json() as Promise<CustomerDashboardDto>;
}

async function cancelBooking(token: string, bookingId: number) {
  const res = await fetch(`${API_BASE}/api/bookings/${bookingId}/cancel`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}` },
    credentials: "include",
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}
```

Wire on customer dashboard routes only. Owner dashboard remains `GET /api/Business/dashboard`.
