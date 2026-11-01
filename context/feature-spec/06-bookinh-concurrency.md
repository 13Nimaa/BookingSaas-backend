# Phase 5 — Booking Concurrency Protection

**Project:** `E:\BookingSaas-backend`

**Prerequisites:**
- Phase 1: Booking Domain Foundation — complete.
- Phase 2: Employee-Service Management API — complete.
- Phase 3: Booking Creation API — complete.
- Phase 4: Booking Availability API — expected to be complete.

Read `AGENTS.md` and the relevant context documentation first. Follow the existing architecture and conventions.

## Objective

Prevent overlapping active bookings for the same employee, including when multiple requests attempt to book the same time concurrently.

Availability is informational. Booking creation must independently enforce the scheduling invariant.

## 1. Scheduling Invariant

For a given employee, two bookings must not overlap when their status is:
- Pending
- Confirmed

The following statuses must not block a time slot:
- Cancelled
- Expired
- Completed

Use half-open intervals `[StartTime, EndTime)`.

Two bookings overlap when:

`existing.StartTime < requested.EndTime && existing.EndTime > requested.StartTime`

Adjacent bookings are allowed.

## 2. Inspect Existing Database and Provider Capabilities

Before choosing an implementation:
- Confirm the database provider is PostgreSQL.
- Inspect the current Booking table, BookingConfiguration, and migrations.
- Inspect the existing Booking creation transaction and persistence flow.
- Determine whether PostgreSQL range types and exclusion constraints can be supported cleanly by the current EF Core/Npgsql versions.

Do not assume that an application-level overlap query is sufficient for concurrency safety.

## 3. Choose a Reliable Concurrency Strategy

Prefer a PostgreSQL database-level solution that guarantees the invariant across concurrent application instances.

Evaluate a PostgreSQL exclusion constraint using:
- EmployeeId equality.
- A timestamp range based on StartTime and EndTime.
- Overlap operator `&&`.
- A partial condition limited to Pending and Confirmed statuses.

Use `[)` range bounds to preserve half-open interval behavior.

If the current schema or EF Core provider cannot support this safely, explain the limitation and implement an appropriate transactional locking strategy instead.

Do not introduce a fragile check-then-insert solution without concurrency protection.

## 4. Booking Creation Integration

Integrate concurrency protection into both existing creation flows:
- Authenticated customer booking.
- Guest booking.

Requirements:
- Preserve all Phase 3 validations.
- Preserve creator tracking.
- Preserve server-side EndTime calculation.
- Do not change existing DTO contracts unnecessarily.
- Return a clear Persian conflict response when the requested time is already occupied.
- Handle database constraint violations without exposing internal PostgreSQL errors.
- Ensure failed booking attempts do not leave orphaned Customer records or partial writes where practical.

## 5. Booking Status Transitions

Ensure future status changes cannot violate the invariant.

For this phase:
- Inspect whether status transitions already exist.
- If they do, integrate the same concurrency invariant into transitions that make a booking active.
- Do not implement the full owner approval or cancellation API if it does not already exist.
- A transition from Cancelled or Expired to Pending/Confirmed must not bypass overlap protection.

## 6. EF Core Migration

If a database-level constraint or supporting schema change is required:
- Create a dedicated migration.
- Ensure `Up` and `Down` are correct.
- Do not apply the migration.
- Do not modify unrelated tables.
- Explain any required PostgreSQL extension, such as `btree_gist`, and whether it must be enabled before the constraint is created.

If using raw SQL in the migration, keep it narrowly scoped and document why it is necessary.

## 7. Tests

Add integration tests using the existing test infrastructure.

Cover:
1. Normal non-overlapping bookings both succeed.
2. Adjacent bookings succeed.
3. Overlapping Pending bookings are rejected.
4. Overlapping Confirmed bookings are rejected.
5. Cancelled bookings do not block new bookings.
6. Expired bookings do not block new bookings.
7. Two concurrent requests for overlapping slots cannot both succeed.
8. Concurrent requests for different employees can both succeed.
9. Conflict responses do not expose database internals.
10. Failed booking attempts do not leave unintended customer or booking records.

Run tests against PostgreSQL, not an in-memory substitute for database concurrency behavior.

## 8. Scope Restrictions

Do NOT implement:
- Booking expiration BackgroundService.
- Full booking lifecycle APIs.
- Owner approval or cancellation endpoints.
- Frontend changes.
- Appointment modifications.
- Unrelated refactoring.

Do not apply migrations.

## 9. Final Report

Report:
1. Chosen concurrency strategy and why.
2. Database constraint or locking details.
3. Migration name and summary.
4. Changes to Booking creation flow.
5. Conflict handling behavior.
6. Integration test results, including concurrent test results.
7. Build result.
8. Any limitations or unresolved issues.

Stop after Phase 5. Do not start another phase automatically.