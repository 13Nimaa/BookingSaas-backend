# Phase 2 — Employee-Service API Upgrade

Implement the API changes required to support employee-specific service pricing and duration.

Phase 1 has introduced the following nullable properties on the existing EmployeeService entity:

- `PriceOverride`
- `DurationMinutesOverride`

Null means the employee inherits the corresponding default value from the Service entity.

## 1. Update Employee-Service GET Endpoint

Existing endpoint:

`GET /api/employees/{employeeId}/services`

Update its response to expose both default and effective values.

Each returned service should include:

- `id`
- `title`
- `defaultPrice`
- `defaultDurationMinutes`
- `priceOverride`
- `durationMinutesOverride`
- `effectivePrice`
- `effectiveDurationMinutes`
- `businessId`

Calculation rules:

- `effectivePrice = PriceOverride ?? Service.Price`
- `effectiveDurationMinutes = DurationMinutesOverride ?? Service.DurationMinutes`

Important:
- Always return the default values from the Service entity.
- Return null for unset overrides.
- Return the calculated effective values.
- Do not modify the original Service entity values.
- Preserve the existing ownership and business-scoping rules.

## 2. Update Employee-Service POST Endpoint

Existing endpoint:

`POST /api/employees/{employeeId}/services`

Extend its request DTO to optionally accept:

```json
{
  "serviceId": 12,
  "priceOverride": 300000,
  "durationMinutesOverride": 45
}
```

Both override fields must be optional and nullable.

Behavior:

- If omitted or null, the employee inherits the Service defaults.
- If provided, save the custom value.
- Validate that provided price values are valid according to the project's existing monetary conventions.
- Validate that provided duration is a positive integer.
- Preserve existing duplicate-assignment protection.
- Preserve business ownership validation.
- Do not copy default Service values into override columns.

Ensure existing clients that only send `{ "serviceId": 12 }` continue to work.

## 3. Add Employee-Service PUT Endpoint

Add:

`PUT /api/employees/{employeeId}/services/{serviceId}`

Purpose: Update the employee-specific pricing and duration.

Request DTO:

```json
{
  "priceOverride": 300000,
  "durationMinutesOverride": 45
}
```

Both fields are nullable.

Behavior:

- A provided numeric value updates that override.
- Explicit null resets that override to inherit the Service default.
- Both fields can be reset independently.
- The endpoint must not modify the original Service entity.
- Validate positive duration and valid price.
- Return the updated employee-service response using the same response shape as GET.

Important implementation detail:

Because both fields support explicit null, ensure the API can distinguish an omitted property from a property explicitly set to null.

If the project's existing JSON DTO conventions do not support this distinction, implement the smallest reliable solution consistent with the current API architecture. Do not silently treat omitted fields as reset requests.

## 4. Authorization and Ownership

Only authorized BusinessOwner users should manage employee-service assignments.

Preserve the existing ownership validation:

- The employee must belong to the authenticated owner's business.
- The service must belong to the same business.
- The EmployeeService relationship must belong to that employee.
- A user must not be able to update another business's assignments by changing route IDs.

Do not weaken existing authorization rules.

## 5. Error Handling

Follow existing project error-handling conventions.

Handle at least:

- Employee not found or not owned: existing appropriate 404/403 behavior.
- Service not found or wrong business: existing appropriate 404/409 behavior.
- Assignment does not exist: 404.
- Duplicate assignment: 409.
- Invalid price or duration: 400 with validation details.

Keep error responses consistent with the existing API.

## 6. Tests

Add or update tests for:

### GET
1. Returns default and effective values when no overrides exist.
2. Returns custom values when overrides exist.
3. Correctly calculates effective values when only one override is set.

### POST
4. Creates an assignment with both overrides null.
5. Creates an assignment with custom price and duration.
6. Supports existing requests containing only `serviceId`.
7. Rejects invalid price or duration.
8. Preserves duplicate and ownership protections.

### PUT
9. Updates both overrides.
10. Updates only the price override.
11. Updates only the duration override.
12. Resets price override to null.
13. Resets duration override to null.
14. Resets both overrides to null.
15. Rejects invalid values.
16. Returns 404 for a nonexistent assignment.
17. Prevents cross-business updates.

## 7. Scope Restrictions

Do not modify:

- Availability calculations.
- Booking creation or duration calculations.
- Booking price snapshot logic.
- Existing Booking or Appointment entities.
- Frontend code.
- Working Hours functionality.

Those changes belong to later phases.

Do not apply migrations or change database schema in this phase. Phase 1 is responsible for the schema migration.

## 8. Completion Criteria

The phase is complete when:

- GET exposes default, override, and effective values.
- POST supports optional overrides.
- PUT supports updating and resetting overrides.
- Existing clients remain backward compatible.
- Authorization and business ownership rules remain intact.
- All relevant tests pass.

At the end, provide a concise report containing:

- Files created or modified.
- Updated request and response contracts.
- Validation and authorization behavior.
- Tests executed and results.
- Any remaining issues.

Do not proceed to Phase 3.
