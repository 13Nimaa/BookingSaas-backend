# Phase 4 — Booking Integration with Employee-Specific Pricing and Duration

Implement employee-specific service pricing and duration in both authenticated and guest booking creation flows.

Phases 1–3 are complete:

- `EmployeeService` supports nullable `PriceOverride` and `DurationMinutesOverride`.
- Employee-Service APIs expose default, override, and effective values.
- Availability uses `DurationMinutesOverride ?? Service.DurationMinutes`.

Booking creation must now use the same effective values as Availability.

## 1. Objective

When a customer books a service with a specific employee, the backend must resolve the effective price and duration from the EmployeeService assignment.

Resolution rules:

```csharp
effectivePrice = employeeService.PriceOverride ?? service.Price;
effectiveDuration = employeeService.DurationMinutesOverride ?? service.DurationMinutes;
```

These values must be used for both authenticated and guest bookings.

## 2. Update Booking Creation

Update both existing endpoints:

- `POST /api/bookings` — authenticated booking.
- `POST /api/bookings/guest` — anonymous guest booking.

Preserve their existing request contracts unless a change is strictly necessary.

The server must:

1. Load and validate the requested Business, Service, and Employee.
2. Verify that the Employee belongs to the requested Business.
3. Verify that the Service belongs to the requested Business.
4. Load the EmployeeService assignment for the selected employee and service.
5. Reject the request if the assignment does not exist.
6. Resolve effective price and duration using the override/fallback rules.
7. Calculate `EndTime` on the server using the effective duration.
8. Persist the final booking price as a snapshot.

Never trust a client-provided `endTime` or price.

The server must calculate both values from database records.

## 3. Booking Price Snapshot

Add a property to the existing Booking entity to store the final price at booking creation.

Preferred property:

- `Price`: use the existing project's monetary type and precision conventions.

Requirements:

- Store the resolved effective price when creating a booking.
- The stored price must remain unchanged if the original Service price changes later.
- The stored price must remain unchanged if the employee's PriceOverride changes later.
- Do not recalculate historical booking prices when reading existing bookings.
- Both authenticated and guest bookings must populate this property.

Do not overwrite historical booking prices during migration.

### Existing Bookings

For existing Booking records, handle the new column safely.

If a database default or nullable transition is required for backward compatibility, choose the smallest safe approach consistent with the existing database conventions.

Do not invent historical prices or populate old bookings using current Service prices or EmployeeService overrides.

Preserve existing booking data.

## 4. Booking Duration and EndTime

Use the resolved effective duration for all new bookings.

Example:

- Service default duration: 30 minutes.
- Employee duration override: 45 minutes.
- Selected start time: 10:00.
- Server-calculated end time: 10:45.

Requirements:

- `EndTime` must always be calculated server-side.
- Preserve the existing DateTime/UTC conventions.
- Preserve business-local timezone handling.
- Ensure the booking interval is consistent with Availability.
- Do not change historical booking start or end times.

## 5. Concurrency and Conflict Protection

Preserve the existing concurrency protection and transaction behavior.

Important:

- Conflict checks must use the newly calculated effective duration.
- Database overlap protection must continue to prevent concurrent conflicting bookings.
- Preserve the existing handling of database exclusion/overlap violations and HTTP 409 responses.
- Do not introduce blind retries.
- Ensure authenticated and guest booking flows use the same conflict-protection logic.

A booking must not be accepted if it conflicts with an existing Pending or Confirmed booking.

Preserve existing booking status and expiration rules.

## 6. Response Contracts

Update the booking response DTO only as needed to expose the stored booking price.

Requirements:

- Return the booking's stored snapshot price, not the current Service price.
- Preserve existing response fields and naming conventions.
- Keep authenticated and guest booking responses consistent.
- Do not expose internal EmployeeService implementation details unnecessarily.

Check existing list and detail DTOs as well. If they expose booking pricing, ensure they return the stored Booking price rather than dynamically reading the current Service price.

Avoid unrelated DTO changes.

## 7. Validation and Error Handling

Preserve existing validation and error response conventions.

Handle:

- Missing employee-service assignment.
- Employee or Service belonging to another business.
- Invalid requested time.
- Booking overlap.
- Invalid booking data.
- Missing or invalid authenticated user where applicable.

Use existing Persian error messages and response conventions.

Do not weaken authorization or guest booking validation.

## 8. Tests

Add or update focused tests covering both authenticated and guest booking flows.

### Duration

1. Authenticated booking uses the default Service duration when no override exists.
2. Authenticated booking uses the employee-specific duration when an override exists.
3. Guest booking uses the default duration when no override exists.
4. Guest booking uses the employee-specific duration when an override exists.
5. Client-provided `endTime` cannot override the server-calculated value.
6. Booking creation rejects an employee-service combination without a valid assignment.

### Price Snapshot

7. Authenticated booking stores the default Service price when no override exists.
8. Authenticated booking stores the employee-specific price when an override exists.
9. Guest booking stores the default Service price when no override exists.
10. Guest booking stores the employee-specific price when an override exists.
11. Changing Service.Price after booking creation does not change the stored booking price.
12. Changing EmployeeService.PriceOverride after booking creation does not change the stored booking price.
13. Existing bookings retain their original data and are not assigned invented historical prices.

### Conflict Protection

14. Overlapping bookings are rejected using the effective duration.
15. Concurrent booking attempts remain protected by existing database constraints.
16. Existing HTTP 409 behavior remains intact.

### Regression

17. Existing authenticated booking behavior remains intact.
18. Existing guest booking behavior remains intact.
19. Existing authorization and ownership rules remain intact.
20. Existing booking status and expiration behavior remain intact.

Use the existing test infrastructure and conventions.

## 9. Database Migration

Create a new EF Core migration for the Booking price snapshot property.

Requirements:

- Preserve existing Booking records.
- Do not invent historical prices.
- Use a safe schema transition for existing rows.
- Preserve existing indexes, constraints, and relationships.
- Do not apply the migration to the database.

I will review and approve migration application separately.

## 10. Scope Restrictions

Do not modify:

- Availability logic unless a directly related consistency bug is discovered.
- Employee-Service API contracts.
- Working Hours.
- Existing Appointment entity.
- Frontend code.
- Unrelated business logic or architecture.

Do not apply any migration.

Do not proceed to Phase 5.

## 11. Completion Criteria

The phase is complete when:

- Both booking creation endpoints use effective employee-specific duration.
- Both booking creation endpoints use effective employee-specific price.
- EndTime is calculated server-side.
- Booking stores a stable price snapshot.
- Historical bookings are preserved without fabricated prices.
- Existing concurrency protection remains intact.
- Relevant tests pass.

At the end, provide a concise report containing:

- Files created or modified.
- Booking entity and migration changes.
- How effective price and duration are resolved.
- How historical bookings are preserved.
- Response DTO changes.
- Tests executed and results.
- Any remaining issues.

Do not proceed to Phase 5 — Integration & Regression.
