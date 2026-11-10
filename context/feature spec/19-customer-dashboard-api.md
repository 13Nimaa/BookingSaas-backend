# Customer Dashboard — Backend Implementation

Implement the backend data required by the Customer Dashboard UI.

Skip rating/score and promotional discount — not in scope.

## 1. Dashboard API

`GET /api/customer/dashboard`

Authorization:
- Any authenticated user.
- Scope bookings by `Customer.UserId` from JWT (same rule as `/api/bookings/mine`).
- Scope favorites by JWT `UserId`.
- Never accept userId / customerId / businessId from the client for authorization.

Return one payload with summary cards, upcoming bookings widget, and recent bookings list.

## 2. Summary cards

| Card | Field | Rule |
|------|-------|------|
| Upcoming | `upcomingBookingsCount` | `Pending` or `Confirmed` and `StartTime >= UtcNow` |
| Total | `totalBookingsCount` | All bookings for this user’s Customer rows |
| Favorite salons | `favoriteSalonsCount` | Count of `BusinessFavorite` rows for the user |

Do **not** return rating/score.

## 3. Upcoming bookings widget

- Same filter as upcoming count.
- Order: `StartTime` asc, then `Id` asc.
- Limit items to **5**; still return full `totalCount`.
- Each item: id, times, status, businessId/name/profileImageUrl, serviceName, employeeName.

## 4. Recent bookings

- All statuses for the user.
- Order: `StartTime` desc, then `Id` desc.
- Limit **5** (full history stays on `GET /api/bookings/mine`).

## 5. Favorites

Minimal salon favorites to power the dashboard card and Favorites nav:

| Method | Path |
|--------|------|
| GET | `/api/customer/favorites` |
| POST | `/api/customer/favorites` body `{ businessId }` |
| DELETE | `/api/customer/favorites/{businessId}` |

Unique `(UserId, BusinessId)`. Idempotent POST if already favorited. Cascade delete with user/business.

## 6. Customer self-cancel

`POST /api/bookings/{bookingId}/cancel`

- Authenticated; booking must belong to caller (`Customer.UserId`).
- Allowed: `Pending` or `Confirmed` → `Cancelled`.
- Otherwise `400`. Wrong owner → `403`. Missing → `404`.

## 7. Customer booking detail

`GET /api/bookings/{bookingId}` already exists for owner/employee.

Extend: any authenticated user may read a booking when `Customer.UserId` matches JWT.

Add `business.profileImageUrl` on `BookingDetailsBusinessDto` (additive).

## 8. Out of scope

- Rating / reviews
- Discount / promo banners
- Notifications badge
- Search (use existing `GET /api/Business?search=`)
- Profile (existing `GET /api/Profile`)
- Reschedule
- Payments

## 9. Migration

`BusinessFavorites` table. Create migration; do **not** apply unless asked.

## 10. Tests

Cover empty dashboard, isolation between customers, upcoming vs past, favorites CRUD + count, cancel happy/forbidden/invalid, customer own detail, anonymous 401.
