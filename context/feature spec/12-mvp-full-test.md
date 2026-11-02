# Phase 11 — Booking MVP Integration & Production Readiness

The core Booking MVP implementation is complete. The goal of this phase is to verify that the existing features work together reliably and that the backend is ready for frontend integration and MVP deployment.

This is an integration and readiness phase, NOT a feature-development phase.

## 1. Inspect the existing implementation

Review the existing project context and the completed Booking implementation:

- Booking domain and EF Core configuration
- Employee-Service management
- Authenticated and guest booking creation
- Availability API
- Booking overlap/concurrency protection
- Automatic Pending expiration
- Booking status management
- Booking listing and filtering
- Booking details
- Customer booking history
- Authentication and authorization
- Existing integration tests and database configuration

Do not repeat previous implementation work unnecessarily.

## 2. End-to-end booking scenarios

Verify these complete flows using the existing API and PostgreSQL integration test infrastructure.

### Scenario A — Authenticated Customer

1. Authenticate as a registered customer.
2. Request availability for a valid service and employee.
3. Select an available slot.
4. Create a booking.
5. Verify the booking is stored with correct CustomerId and CreatorType.
6. Verify EndTime is calculated from Service.DurationMinutes.
7. Verify the booking appears in the owner's booking list.
8. Verify the booking appears in the assigned employee's list.
9. Verify the customer can retrieve it through booking history.
10. Verify booking details are correct.

### Scenario B — Guest Customer

1. Create a guest booking.
2. Verify CreatorType is Guest.
3. Verify CreatedByUserId is null.
4. Verify guest customer information is stored correctly.
5. Verify the booking appears in the owner's and assigned employee's lists.

### Scenario C — Owner Status Management

1. Owner confirms a Pending booking.
2. Verify the status changes to Confirmed.
3. Owner completes the Confirmed booking.
4. Verify the status changes to Completed.
5. Verify invalid transitions are rejected.

### Scenario D — Booking Expiration

1. Create a Pending booking with CreatedAt older than 30 minutes.
2. Execute the expiration process.
3. Verify the status becomes Expired.
4. Verify Confirmed bookings are not expired.
5. Verify expired bookings no longer block availability.

### Scenario E — Overlapping Bookings

1. Create a booking for an employee and time range.
2. Attempt to create an overlapping Pending or Confirmed booking.
3. Verify the second booking is rejected.
4. Verify adjacent bookings are allowed.
5. Verify Cancelled and Expired bookings no longer block the slot.

## 3. Authorization verification

Verify that:
- Owner can access only their own business bookings.
- Employee can access only their assigned bookings.
- Customer can access only their own booking history.
- Guest booking creation follows existing anonymous endpoint rules.
- Customers cannot use owner-only status management.
- Employees cannot approve or modify booking statuses.
- Users cannot access another business's booking details.
- No endpoint trusts client-supplied ownership identifiers.

## 4. API contract consistency

Review the Booking API responses and ensure:
- DTO field naming is consistent.
- Date/time serialization is consistent.
- Error response structure is consistent.
- Persian user-facing validation and error messages are used.
- HTTP status codes are appropriate.
- Pagination response shape is consistent.
- No EF entities or sensitive Identity information are exposed.

Do not change API contracts unless an actual inconsistency or bug is found.

## 5. Database and migration readiness

- Inspect all Booking-related migrations.
- Check whether migrations are ordered correctly.
- Check that the database schema can be created from the existing migration history.
- Verify PostgreSQL-specific requirements such as extensions and exclusion constraints.
- Verify required indexes and foreign keys.
- Verify that all required configuration values are documented.
- Do NOT apply migrations to any production database.
- Do NOT reset or delete database data.

If a migration problem is discovered, report it and propose the smallest safe correction.

## 6. Production configuration

Check:
- Connection string configuration.
- JWT secret configuration.
- CORS configuration.
- Environment-specific settings.
- Logging and exception handling.
- BackgroundService registration and shutdown behavior.
- PostgreSQL connection behavior.
- Swagger/OpenAPI availability and environment restrictions.

Do not expose secrets or copy credentials into source files.

Do not introduce new infrastructure or paid services.

## 7. Testing

Run:
- All existing Booking-related tests.
- All existing backend tests.
- Relevant PostgreSQL integration tests.
- Build in Release configuration.

Do not replace real PostgreSQL integration tests with in-memory tests for PostgreSQL-specific behavior.

Do not claim a scenario is verified unless an actual test or reproducible API test was executed.

## 8. Scope restrictions

Do NOT add:
- Rescheduling.
- Customer cancellation.
- Notifications.
- Audit logging.
- Statistics.
- New booking features.
- Frontend changes.
- Unnecessary refactoring.
- Unnecessary migrations.

Only fix concrete bugs discovered during verification.

## 9. Final report

Provide:

1. End-to-end scenarios verified.
2. Test results and exact test counts.
3. Release build result.
4. Migration and PostgreSQL readiness.
5. Production configuration findings.
6. Bugs discovered and fixes applied.
7. Remaining blockers, if any.
8. Final verdict: Ready for frontend integration, Ready for MVP deployment, or Not Ready.

Be precise. Distinguish verified behavior from items that were only inspected.