# Phase 8 — Automatic Pending Booking Expiration

Implement automatic expiration for Pending bookings after 30 minutes.

## 1. Inspect existing implementation

Read the project context and inspect:
- Booking entity and BookingStatus enum
- BookingService and existing booking creation flow
- CreatedAt initialization and UTC conventions
- DbContext and EF Core configuration
- Existing BackgroundService / hosted service patterns
- PostgreSQL and Npgsql configuration
- Existing dependency injection registrations
- Existing tests and test infrastructure

Do not redesign completed phases or modify unrelated code.

## 2. Expiration rules

A booking must transition from Pending to Expired when:

CreatedAt <= current UTC time - 30 minutes

Rules:
- Only Pending bookings are eligible.
- Confirmed, Cancelled, Completed, and Expired bookings must remain unchanged.
- Use CreatedAt as the source of truth.
- Use UTC consistently.
- Do not use StartTime to determine expiration.
- Do not change the booking's other fields.
- Make the expiration threshold configurable through existing configuration conventions if appropriate, with a default of 30 minutes.

## 3. Background processing

Implement a hosted BackgroundService that periodically finds and expires eligible bookings.

Requirements:
- Register it using the existing dependency injection conventions.
- Use IServiceScopeFactory to create a scope and resolve the DbContext or appropriate service.
- Use CancellationToken correctly.
- Do not keep a scoped DbContext alive inside the BackgroundService constructor.
- Use a reasonable configurable polling interval, with a default of 1 minute.
- Log the number of expired bookings and unexpected failures.
- A failure in one execution must not permanently stop future executions.
- Respect application shutdown.

## 4. Atomicity and multi-instance safety

The application may run on multiple instances.

Expiration must be safe when multiple instances execute the background task simultaneously.

Prefer a single atomic PostgreSQL UPDATE statement:

UPDATE "Bookings"
SET "Status" = @expired
WHERE "Status" = @pending
  AND "CreatedAt" <= @expirationCutoff;

Use the actual table/schema names, enum storage format, and EF Core/Npgsql conventions in this project.

Do not load all eligible bookings into memory and update them one by one.

The operation must only transition rows that are still Pending at the time of the update.

Do not introduce unnecessary locking or a distributed lock if the atomic update is sufficient.

## 5. Time handling

- Obtain the current time in UTC using the project's existing time abstraction if one exists.
- Otherwise use an appropriate UTC clock API.
- Calculate the cutoff once per execution.
- Do not use local machine time.
- Ensure CreatedAt is consistently stored in UTC.

## 6. Testing

Add tests for:

1. Pending booking older than 30 minutes becomes Expired.
2. Pending booking exactly at the expiration cutoff becomes Expired.
3. Pending booking younger than 30 minutes remains Pending.
4. Confirmed booking older than 30 minutes remains Confirmed.
5. Cancelled booking remains unchanged.
6. Completed booking remains unchanged.
7. Already Expired booking remains unchanged.
8. Multiple executions are idempotent.
9. Multiple service instances executing concurrently do not cause incorrect transitions.
10. CancellationToken is respected.

Use the existing test framework and conventions.

Use PostgreSQL integration tests for behavior that depends on PostgreSQL-specific implementation. Do not claim concurrency guarantees based only on in-memory tests.

## 7. Scope restrictions

Do NOT implement:
- Booking approval or cancellation endpoints.
- Customer self-cancellation.
- Employee status management.
- Notifications or reminders.
- Booking rescheduling.
- Frontend changes.
- Changes to the legacy Appointment entity.
- Migrations unless a genuine schema requirement is discovered.

Do not apply migrations.

## 8. Final report

Report:
- Files created or modified.
- BackgroundService implementation and polling interval.
- How expiration is made atomic and safe across multiple instances.
- Configuration keys and defaults.
- Tests executed and results.
- Whether a migration is required.
- Any unresolved limitations.

Do not claim tests passed unless they were actually executed.