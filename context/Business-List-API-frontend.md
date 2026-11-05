# Business list API — frontend guide

Public business discovery with search, city filter, sort, and pagination.

**Endpoint:** `GET /api/Business`  
**Auth:** none (public)

> Breaking change: response is now `PagedResult<BusinessDto>`, not a bare array.

---

## Query params

| Param | Type | Default | Notes |
|-------|------|---------|--------|
| `search` | string | — | Case-insensitive; matches **name**, **city**, **address**, **description** |
| `city` | string | — | Case-insensitive **exact** city filter (e.g. `Tehran`) |
| `sortBy` | string | `name` | Allowed: `name`, `city`, `id` |
| `sortOrder` | string | `asc` | Allowed: `asc`, `desc` |
| `page` | int | `1` | ≥ 1 |
| `pageSize` | int | `20` | 1…100 |

Invalid `sortBy` / `sortOrder` / paging → `400` (FluentValidation / ProblemDetails).

---

## Types

```ts
type BusinessDto = {
  id: number;
  name: string;
  city: string;
  address: string;
  description: string | null;
  profileImageUrl: string | null;
  ownerId: string;
};

type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
};

type BusinessListQuery = {
  search?: string;
  city?: string;
  sortBy?: "name" | "city" | "id";
  sortOrder?: "asc" | "desc";
  page?: number;
  pageSize?: number;
};
```

**200** → `PagedResult<BusinessDto>`

---

## Example calls

```http
GET /api/Business
GET /api/Business?city=Tehran
GET /api/Business?search=salon&sortBy=name&sortOrder=asc
GET /api/Business?city=Shiraz&page=2&pageSize=10
GET /api/Business?search=clinic&city=Tehran&sortBy=city&sortOrder=desc&page=1&pageSize=20
```

```ts
async function listBusinesses(query: BusinessListQuery = {}) {
  const params = new URLSearchParams();
  if (query.search) params.set("search", query.search);
  if (query.city) params.set("city", query.city);
  if (query.sortBy) params.set("sortBy", query.sortBy);
  if (query.sortOrder) params.set("sortOrder", query.sortOrder);
  if (query.page) params.set("page", String(query.page));
  if (query.pageSize) params.set("pageSize", String(query.pageSize));

  const qs = params.toString();
  const res = await fetch(`${API_BASE}/api/Business${qs ? `?${qs}` : ""}`);
  if (!res.ok) throw new Error(await res.text());
  return (await res.json()) as PagedResult<BusinessDto>;
}
```

---

## FE checklist

1. **Stop treating the response as an array** — use `data.items`.
2. Debounce `search` (~300ms); reset `page` to `1` when search/city/sort changes.
3. City filter: exact match UI (dropdown / chips). Free-text city still works via `search`.
4. Pagination: use `hasNextPage` / `hasPreviousPage` or `totalPages`; keep `pageSize` ≤ 100.
5. Empty state: `items.length === 0` with `totalCount === 0` is valid (not an error).
6. Detail page unchanged: `GET /api/Business/{id}` → single `BusinessDto`.

---

## Related

| Topic | Doc |
|-------|-----|
| Booking / employee-service contracts | `context/Booking-MVP-frontend.md` |
| Backend inventory | `context/PROJECT_INVENTORY.md` |
