# Phase 10 — Customer Booking History API

Implement an authenticated customer booking history endpoint in the existing BookingSaaS backend.

## 1. Inspect existing implementation

Inspect:
- Booking entity and Customer relationship
- BookingController and BookingService
- BookingListQuery and BookingListItemDto
- BookingDetailsDto
- CustomerService and customer-to-user relationship
- Existing pagination and validation conventions
- Authentication and authorization patterns
- Existing integration tests

Reuse existing DTOs, pagination models, query logic, and service architecture wherever appropriate.

Do not duplicate existing functionality unnecessarily.

## 2. Endpoint

Implement:

GET /api/bookings/mine

Optional query parameters:
- status
- fromDate
- toDate
- page
- pageSize

Use the existing pagination defaults and limits:
- page = 1
- pageSize = 20
- maximum pageSize = 100

## 3. Data isolation and ownership

- Require authentication.
- Resolve the customer using the authenticated user's ID from JWT claims.
- Return only bookings belonging to that authenticated customer.
- Never trust CustomerId or UserId supplied by the client.
- Do not return guest bookings belonging to other people.
- Do not automatically associate guest bookings with registered accounts based only on matching phone numbers.
- If the authenticated user has no Customer record, return an appropriate empty result or existing project-standard response. Inspect existing conventions and choose consistently.
- Do not allow BusinessOwner or Employee users to use this endpoint as a way to access other customers' booking history.

Important:
Inspect the actual Customer.UserId and Booking.CustomerId relationship before implementation. Do not assume every customer has exactly one Customer record across all businesses.

## 4. Filtering

Apply filters server-side using IQueryable before counting and pagination.

- status: exact status match.
- fromDate: inclusive lower boundary for Booking.StartTime.
- toDate: inclusive upper boundary for Booking.StartTime.
- Validate date ranges.
- Reject undefined status values.
- Follow the existing UTC date conventions used by GET /api/bookings.
- Do not load all bookings into memory.

## 5. Response

Reuse BookingListItemDto and PagedResult if they contain the appropriate information.

Return relevant fields:
- booking ID
- startTime and endTime
- status
- creatorType
- business name
- service name and duration
- employee name
- other existing fields that are appropriate for a customer-facing booking history

Do not expose:
- Other customers' information
- Internal Identity fields
- Passwords or tokens
- Unnecessary database fields

If BookingListItemDto exposes fields that should not be shown to customers, create a separate customer-facing DTO instead of exposing unnecessary data.

## 6. Sorting and pagination

- Sort by StartTime descending.
- Use Id as a secondary descending sort key for deterministic ordering.
- Apply filters before Count, Skip, and Take.
- Use AsNoTracking.
- Project directly to DTOs.
- Avoid N+1 queries.

## 7. Tests

Add tests for:

1. Customer sees their own bookings.
2. Customer does not see another customer's bookings.
3. Customer sees bookings across multiple businesses if their Customer records exist in multiple businesses.
4. Guest bookings are not automatically associated through phone matching.
5. Customer with no Customer record gets the expected response.
6. Filtering by status.
7. Filtering by fromDate.
8. Filtering by toDate.
9. Combined filters.
10. Default pagination.
11. Custom pagination.
12. Maximum pageSize validation.
13. Invalid page values.
14. Invalid date range.
15. Undefined status value.
16. Anonymous access is rejected.
17. BusinessOwner and Employee cannot use this endpoint to access customer history.
18. Response does not expose another customer's data or unnecessary sensitive fields.

Use the existing test framework and PostgreSQL integration test conventions.

## 8. Scope restrictions

Do NOT implement:
- Customer cancellation.
- Booking rescheduling.
- Booking creation changes.
- Booking status management changes.
- Guest booking lookup.
- Phone verification or account linking.
- Notifications.
- Frontend changes.
- Changes to the legacy Appointment entity.

Do not apply migrations.

## 9. Final report

Report:
- Endpoint and query parameter examples.
- How customer identity and booking ownership are resolved.
- Whether customers with records across multiple businesses are supported.
- DTO and pagination response shape.
- Files created or modified.
- Tests executed and results.
- Whether a migration is required.
- Any limitations.

Do not claim tests passed unless they were actually executed.