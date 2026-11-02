# Phase 3 — Availability Integration with Employee-Specific Service Duration

Implement support for employee-specific service duration overrides in the existing Booking Availability API.

Phase 1 introduced:
- `EmployeeService.PriceOverride`
- `EmployeeService.DurationMinutesOverride`

Phase 2 introduced:
- `EffectivePrice`
- `EffectiveDurationMinutes`
- Updated Employee-Service GET/POST/PUT endpoints.

## 1. Objective

The Availability API must calculate available booking slots using the effective duration of the selected service for the selected employee.

Duration resolution:

`EffectiveDurationMinutes = DurationMinutesOverride ?? Service.DurationMinutes`

Examples:

- Default service duration: 30 minutes
- Employee-specific duration: 45 minutes
- Availability must use 45 minutes.

If no duration override exists, use the original Service duration.

## 2. Update Existing Availability Logic

Update the existing endpoint:

`GET /api/bookings/availability`

Keep its existing route, query parameters, response contract, and authorization behavior.

The endpoint must:

1. Validate the business, service, and employee as it currently does.
2. Verify that the selected employee belongs to the requested business.
3. Verify that the selected service belongs to the requested business.
4. Verify that the employee has an EmployeeService assignment for the requested service.
5. Resolve the effective duration from the EmployeeService relationship.
6. Use that effective duration throughout the availability calculation.

Do not rely only on the existence of Employee and Service entities. The EmployeeService assignment is required.

## 3. Availability Calculation

Use the resolved effective duration consistently for:

- Calculating each slot's end time.
- Checking whether a slot fits within employee working hours.
- Checking overlap with existing bookings.
- Determining whether enough time remains before the end of the working period.

Example:

- Employee working hours: 09:00–12:00
- Service default duration: 30 minutes
- Employee duration override: 45 minutes

Every returned slot must represent a valid 45-minute booking.

Do not generate slots that extend beyond working hours.

Preserve the existing slot interval configuration (for example, 15-minute intervals). Do not change the interval just because the service duration changes.

## 4. Existing Booking Conflict Rules

Preserve the current booking conflict behavior.

In particular:

- Pending and Confirmed bookings must block availability.
- Cancelled, Completed, and Expired bookings must not block availability, according to the existing implementation.
- Existing booking start and end times must be used for overlap checks.
- Do not recalculate historical booking durations using the current EmployeeService configuration.

Do not modify booking status transitions or expiration behavior.

## 5. Default Duration Compatibility

Ensure existing EmployeeService assignments with null `DurationMinutesOverride` continue to work using `Service.DurationMinutes`.

Do not require every employee to have an explicit duration override.

Do not copy default Service values into EmployeeService override fields.

## 6. Price Handling

Do not change pricing behavior in this phase.

`PriceOverride` and `EffectivePrice` are outside this phase's scope.

Price snapshots and booking price calculations will be handled in the Booking integration phase.

## 7. Error Handling

Follow the existing API error-handling conventions.

Handle these cases consistently:

- Employee does not exist.
- Service does not exist.
- Employee does not belong to the requested business.
- Service does not belong to the requested business.
- Employee has no assignment for the selected service.

Do not expose cross-business data.

Do not introduce new error response formats without a clear reason.

## 8. Tests

Add or update focused Availability tests covering:

1. Employee without a duration override uses the default Service duration.
2. Employee with a duration override uses the custom duration.
3. Only the duration override is used when the price override is null.
4. Slots are not returned if their effective end time exceeds working hours.
5. Existing bookings block slots using their stored start and end times.
6. Pending and Confirmed bookings block availability.
7. Cancelled, Completed, and Expired bookings do not block availability.
8. An employee without an EmployeeService assignment cannot receive availability for that service.
9. Cross-business employee/service combinations are rejected.
10. Existing availability behavior remains intact for assignments without overrides.

Use the project's existing test infrastructure and conventions.

## 9. Scope Restrictions

Do not modify:

- Booking Creation API.
- Booking entity or booking database schema.
- Booking price snapshot logic.
- Employee-Service GET/POST/PUT/DELETE contracts.
- Working Hours management.
- Frontend code.
- Existing Appointment entity.

Do not apply any database migration.

Do not perform unrelated refactoring.

## 10. Completion Criteria

The phase is complete when:

- Availability uses the employee-specific effective duration.
- Default duration behavior remains backward compatible.
- Employee-Service assignment is validated.
- Existing booking conflict rules remain unchanged.
- All relevant tests pass.

At the end, provide a concise report containing:

- Files created or modified.
- How effective duration is resolved.
- Which parts of availability calculation were updated.
- Tests executed and their results.
- Any remaining issues.

Do not proceed to Phase 4 — Booking Integration.
