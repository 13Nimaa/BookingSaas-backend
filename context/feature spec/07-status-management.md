# Phase 6 — Booking Lifecycle & Status Management API

Implement Phase 6 of the BookingSaaS backend: booking status management.

## 1. First, understand the existing implementation

Read the relevant project context and inspect the existing:
- Booking entity and BookingStatus enum
- BookingController
- BookingService and its interfaces
- Authentication and authorization conventions
- Business ownership authorization patterns
- Existing DTOs, validators, exception handling, and API response conventions
- Existing booking creation and concurrency protection implementation

Do not redesign existing architecture or rewrite completed phases.

## 2. Required functionality

Implement an API that allows an authenticated Business Owner to update the status of a booking belonging to their own business.

Use the existing controller and service architecture.

Suggested endpoint:

PATCH /api/bookings/{bookingId}/status

Request DTO:

{
  "status": "Confirmed"
}

Follow the project's existing enum serialization and DTO conventions. Do not assume that string enum serialization is enabled; inspect the current configuration first.

## 3. Authorization rules

- Only authenticated Business Owners can change booking statuses.
- The booking must belong to a business owned by the authenticated user.
- A Business Owner must never be able to update bookings belonging to another business.
- Employees must not be allowed to approve or modify booking statuses.
- Customers and anonymous users must not be allowed to use this endpoint.
- Follow existing authorization conventions rather than introducing a new authorization architecture.

## 4. Allowed status transitions

Implement exactly these transitions:

- Pending → Confirmed
- Pending → Cancelled
- Confirmed → Completed
- Confirmed → Cancelled

Reject all other transitions.

Additional rules:
- Cancelled is terminal.
- Completed is terminal.
- Expired is terminal.
- Expired must not be manually assigned through this endpoint.
- Do not allow clients to bypass transition validation by sending arbitrary enum values.
- Reject undefined enum values.

Return an appropriate Persian error message for invalid transitions.

## 5. Data integrity and concurrency

- Load the booking from the database.
- Verify business ownership before modifying it.
- Update only the Status field.
- Preserve all existing booking information.
- Consider concurrent status-update requests and use the project's existing EF Core/database concurrency conventions if available.
- Do not introduce unnecessary database changes.
- Do not modify the legacy Appointment entity.

## 6. API responses

Follow existing API response conventions.

Handle these cases appropriately:
- Booking not found.
- User is not authorized to modify this booking.
- Invalid requested status.
- Invalid status transition.
- Successful status update.

Use appropriate HTTP status codes, such as 200, 400, 403, and 404, according to existing project conventions.

All user-facing error messages must be Persian.

Do not expose database or exception details in API responses.

## 7. Tests

Add tests covering:

1. Owner successfully confirms a Pending booking.
2. Owner successfully cancels a Pending booking.
3. Owner successfully completes a Confirmed booking.
4. Owner successfully cancels a Confirmed booking.
5. Owner cannot modify a booking from another business.
6. Employee cannot modify booking status.
7. Customer cannot modify booking status.
8. Cancelled booking cannot be modified.
9. Completed booking cannot be modified.
10. Expired booking cannot be modified.
11. Pending → Completed is rejected.
12. Confirmed → Pending is rejected.
13. Undefined status values are rejected.
14. Nonexistent booking returns the correct response.

Use the existing test framework and conventions.

## 8. Scope restrictions

Do NOT implement:
- Automatic expiration BackgroundService.
- Automatic cancellation or reminder notifications.
- Customer self-cancellation.
- Employee status management.
- Booking rescheduling.
- Booking deletion.
- Frontend changes.
- New migrations unless a genuine database requirement is identified.

Do not apply migrations.

## 9. Final report

After implementation, report:
- Files created or modified.
- Endpoint and request/response examples.
- Authorization behavior.
- Implemented transition rules.
- Tests executed and their results.
- Whether a migration is required.
- Any limitations or unresolved issues.

Do not claim tests passed unless they were actually executed.