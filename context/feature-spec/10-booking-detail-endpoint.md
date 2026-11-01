# Phase 9 — Booking Details API

Implement a read-only booking details endpoint in the existing BookingSaaS backend.

## 1. Inspect existing implementation

Inspect:
- Booking entity and its relationships
- BookingController and BookingService
- BookingListItemDto and existing DTO conventions
- Business Owner and Employee authorization logic
- Existing API response conventions
- Existing integration tests

Reuse existing authorization and service patterns. Do not duplicate or redesign completed phases.

## 2. Endpoint

Implement:

GET /api/bookings/{bookingId}

The endpoint must return the details of one booking.

## 3. Authorization

BusinessOwner:
- Can access bookings belonging to businesses they own.

Employee:
- Can access only bookings assigned to their own Employee record.
- Supplying another booking ID must never expose another employee's booking.

Customer:
- Must not access this endpoint.

Anonymous:
- Must not access this endpoint.

Follow existing project conventions for unauthorized and forbidden responses.

Do not trust BusinessId, EmployeeId, or UserId supplied by the client.

## 4. Response DTO

Create a dedicated BookingDetailsDto.

Inspect the actual entities and return relevant fields available in the current domain model.

Include, where available and appropriate:

Booking:
- Id
- BusinessId
- StartTime
- EndTime
- Status
- CreatorType
- CreatedAt
- Notes

Customer:
- Id
- Name
- PhoneNumber

Service:
- Id
- Name
- Description, if available
- DurationMinutes
- Price, if available

Employee:
- Id
- Name
- Profile image, if available

Business:
- Id
- Name

Do not invent properties that do not exist.

Do not expose:
- Passwords or authentication data
- Internal Identity fields
- Refresh tokens
- Unnecessary navigation properties
- Sensitive internal database information

Follow existing JSON naming and nullability conventions.

## 5. Query and performance

- Use AsNoTracking for this read-only query.
- Prefer direct DTO projection over loading complete entity graphs.
- Avoid N+1 queries.
- Return only the required fields.
- Reuse the existing BookingService architecture.

## 6. Error handling

Handle:
- Booking not found.
- Booking exists but is outside the authenticated user's scope.
- Successful retrieval.

Follow existing Persian API error message conventions.

Do not expose internal exception details.

## 7. Tests

Add tests for:

1. Owner can retrieve a booking from their business.
2. Owner cannot retrieve another business's booking.
3. Employee can retrieve their own assigned booking.
4. Employee cannot retrieve another employee's booking.
5. Customer cannot access booking details.
6. Anonymous request is rejected.
7. Nonexistent booking returns the correct response.
8. Response contains the expected booking, customer, service, employee, and business fields.
9. Response does not expose sensitive Identity information.

Use the existing test framework and PostgreSQL integration test conventions.

## 8. Scope restrictions

Do NOT implement:
- Booking updates.
- Booking deletion.
- Rescheduling.
- Customer booking history.
- New status transitions.
- Notifications.
- Frontend changes.
- Changes to the legacy Appointment entity.

Do not apply migrations.

## 9. Final report

Report:
- Endpoint and response example.
- Authorization behavior.
- DTO fields.
- Files created or modified.
- Tests executed and results.
- Whether a migration is required.
- Any limitations.

Do not claim tests passed unless they were actually executed.