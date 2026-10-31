# Customer CRM API — frontend guide

Base path: `api/business/{businessId}/customers`  
Auth: JWT `Authorization: Bearer <token>`  
Allowed roles: **Admin**, **BusinessOwner** (BusinessOwner only for their own business)

JSON uses **camelCase**. Dates are ISO-8601 UTC (e.g. `"2026-10-15T10:30:00Z"`). Convert Jalali ↔ Gregorian on the frontend when showing/sending calendar dates.

---

## Types

### `Customer`

```ts
type Customer = {
  id: number;
  name: string;
  phoneNumber: string | null;
  email: string | null;
  notes: string | null;
  isActive: boolean;
  createdAt: string;       // ISO datetime
  updatedAt: string | null;
  businessId: number;
};
```

### `PagedCustomers`

```ts
type PagedCustomers = {
  items: Customer[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;      // 0 when totalCount is 0
};
```

---

## Endpoints

### 1. List (search / filter / sort / paginate)

`GET /api/business/{businessId}/customers`

| Query | Type | Default | Notes |
|-------|------|---------|--------|
| `search` | string? | — | Partial match on name, phone, or email. Trimmed server-side. Omit if empty. |
| `isActive` | bool? | — | `true` / `false`. Omit to get both. |
| `createdFrom` | datetime? | — | Inclusive start (`CreatedAt >= value`). Prefer `YYYY-MM-DD` or full ISO. |
| `createdTo` | datetime? | — | Inclusive **calendar day** (server includes the whole day, not only midnight). |
| `sortBy` | string? | `createdAt` | Whitelist only: `createdAt` \| `name` \| `updatedAt` |
| `sortOrder` | string? | `desc` | `asc` \| `desc` |
| `page` | number | `1` | ≥ 1 |
| `pageSize` | number | `20` | 1…100 |

**Example**

```http
GET /api/business/12/customers?search=علی&isActive=true&createdFrom=2026-10-01&createdTo=2026-10-31&sortBy=name&sortOrder=asc&page=1&pageSize=20
Authorization: Bearer <token>
```

**200 response**

```json
{
  "items": [
    {
      "id": 1,
      "name": "علی رضایی",
      "phoneNumber": "09123456789",
      "email": "ali@example.com",
      "notes": "مشتری دائمی",
      "isActive": true,
      "createdAt": "2026-10-15T10:30:00Z",
      "updatedAt": null,
      "businessId": 12
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 45,
  "totalPages": 3
}
```

**Breaking change:** this endpoint no longer returns a bare `Customer[]`. Always read `items` + pagination fields.

**UI tips**

- Debounce `search` (~300ms); reset `page` to `1` when search/filters change.
- Date range pickers: send Gregorian; do not send `createdFrom` > `createdTo` (API returns 400).
- For “inactive only”, use `isActive=false` — soft-deleted customers stay in the list as inactive.
- Empty page → `items: []` with correct `totalCount` / `totalPages`.

---

### 2. Get one

`GET /api/business/{businessId}/customers/{customerId}`

**200** → `Customer`  
**404** → not found or wrong business

---

### 3. Create

`POST /api/business/{businessId}/customers`  
`Content-Type: application/json`

```json
{
  "name": "علی رضایی",
  "phoneNumber": "09123456789",
  "email": "ali@example.com",
  "notes": "مشتری دائمی"
}
```

| Field | Required | Notes |
|-------|----------|--------|
| `name` | yes | Max 150 |
| `phoneNumber` | no | Iranian mobile: `09xxxxxxxxx`, `9xxxxxxxxx`, or `+989xxxxxxxxx` |
| `email` | no | Valid email if provided; max 256 |
| `notes` | no | Max 2000 |

Do **not** send `id`, `businessId`, `isActive`, `createdAt`, or `updatedAt`.  
Server sets `isActive: true`, `createdAt: now`, `updatedAt: null`.

**201** → `Customer` (Location header points at get-by-id)

---

### 4. Update

`PUT /api/business/{businessId}/customers/{customerId}`

```json
{
  "name": "علی رضایی",
  "phoneNumber": "09123456789",
  "email": "ali@example.com",
  "notes": "مشتری VIP",
  "isActive": true
}
```

Same field rules as create, plus required `isActive` (boolean).  
Do **not** send `id` or `businessId`. Server updates `updatedAt`.

**200** → `Customer`  
**404** → not found / wrong business

Use this (not DELETE) to **reactivate** a soft-deleted customer: `isActive: true`.

---

### 5. Delete (soft)

`DELETE /api/business/{businessId}/customers/{customerId}`

Sets `isActive: false`. Does **not** remove the row or related bookings.

**204** → empty body  
**404** → not found / wrong business

After delete, refresh the list (or patch local row to `isActive: false`). Prefer confirm UI: “غیرفعال کردن مشتری”.

---

## Errors

| Status | When |
|--------|------|
| `400` | Validation failed (body or query). Messages are often Persian. |
| `401` | Missing/invalid token |
| `403` | Authenticated but not Admin/owner of this business |
| `404` | Business or customer not found (or customer not in that business) |

Validation example shape (ASP.NET / FluentValidation):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["نام الزامی است."],
    "SortBy": ["مرتب‌سازی فقط بر اساس createdAt، name یا updatedAt مجاز است."]
  }
}
```

Map `errors` field keys to form fields (`Name` → `name`, etc.).

---

## Suggested frontend modules

1. **Customers list page** — table + search input + status filter + date range + sort headers + pagination (`page` / `pageSize`).
2. **Create / edit form** — name, phone, email, notes; edit also toggles `isActive`.
3. **Detail drawer/page** — `GET` by id; actions: edit, soft-delete, reactivate.
4. **API client** — wrap all five calls; always pass current `businessId` from app context/route.

Example fetch helper:

```ts
async function listCustomers(
  businessId: number,
  params: {
    search?: string;
    isActive?: boolean;
    createdFrom?: string;
    createdTo?: string;
    sortBy?: "createdAt" | "name" | "updatedAt";
    sortOrder?: "asc" | "desc";
    page?: number;
    pageSize?: number;
  },
  token: string
): Promise<PagedCustomers> {
  const q = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== "") q.set(k, String(v));
  });
  const res = await fetch(
    `${API_BASE}/api/business/${businessId}/customers?${q}`,
    { headers: { Authorization: `Bearer ${token}` }, credentials: "include" }
  );
  if (!res.ok) throw await res.json();
  return res.json();
}
```

---

## Checklist

1. Auth header on every call; only show CRM UI to Admin / BusinessOwner.
2. List uses **paged** response (`items`, not raw array).
3. Reset to page 1 when filters/search/sort change.
4. Clamp `pageSize` ≤ 100 client-side too.
5. Create/update validation mirrors server (phone regex, email optional, name required).
6. Delete = soft-delete; offer reactivate via PUT `isActive: true`.
7. Jalali UI dates → Gregorian ISO before `createdFrom` / `createdTo`.
8. Never put `businessId` in the body — it comes from the route only.
