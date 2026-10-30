# Working hours API — frontend update

Base path: `api/business/{businessId}/employees/{employeeId}/working-hours`  
Auth: `Employee` or `BusinessOwner`

## What changed

- Working hours are **tied to a specific calendar date**, not only a weekday.
- Send/receive **Gregorian ISO** `yyyy-MM-dd` (e.g. `"2026-10-30"`).
- Convert **Jalali ↔ Gregorian on the frontend** before/after API calls.
- `dayOfWeek` is **no longer sent** on create/update; the API sets it from `date` and returns it.

## Create / Update body

```json
{
  "date": "2026-10-30",
  "startTime": "09:00:00",
  "endTime": "17:00:00"
}
```

| Field | Type | Notes |
|-------|------|--------|
| `date` | string | Gregorian ISO. Convert from Jalali before send. |
| `startTime` | string | `HH:mm:ss` (or `HH:mm`) |
| `endTime` | string | Must be after `startTime` |

Do **not** send `dayOfWeek`.

## Response item (`WorkingHourDto`)

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

| Field | Notes |
|-------|--------|
| `date` | Gregorian ISO — convert to Jalali for UI |
| `dayOfWeek` | 0=Sunday … 6=Saturday (derived) |

Create → `201` `{ message, data }`  
Update → `200` `{ message, data }`  
Get all → `200` array of items above (ordered by `date`, then `startTime`)  
Delete → `200` `{ message }`

## Errors (unchanged meaning)

| Status | When |
|--------|------|
| `400` | `endTime` ≤ `startTime` |
| `409` | Overlap with another range **on the same date** |
| `404` | Employee / working hour not found (or no access) |
| `401` | Not authenticated |

## Frontend checklist

1. Date picker stays Jalali in UI.
2. On submit: Jalali → Gregorian ISO → put in `date`.
3. On display: `date` → Jalali.
4. Remove `dayOfWeek` from create/update payloads.
5. Overlap is per **date**, not per weekday.