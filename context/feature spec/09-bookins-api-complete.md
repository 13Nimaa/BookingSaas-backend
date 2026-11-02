# Phase 8 — Booking Listing, Filtering, Search & Pagination API

Implement booking listing for Business Owners and Employees in the existing BookingSaaS backend.

## 1. Inspect existing implementation

Read the existing project context and inspect:
- Booking entity and its relationships
- BookingController and BookingService
- Business ownership authorization
- Employee identity and Business relationship
- Customer and Service entities
- Booking status lifecycle implementation
- Existing DTO, validation, pagination, and API response conventions
- Existing integration test infrastructure

Do not redesign completed phases or duplicate existing functionality.

If an existing pagination DTO or response wrapper is available, reuse it.

## 2. Endpoint

Implement:

GET /api/bookings

Support these optional query parameters:

- status
- employeeId
- customerId
- serviceId
- fromDate
- toDate
- search
- page
- pageSize

Do not introduce a separate endpoint unless the existing project architecture requires one.

## 3. Authorization and data isolation

Business Owner:
- Can only see bookings belonging to businesses they own.
- If the owner has multiple businesses, follow the existing ownership model and determine the appropriate business scope.
- Must never access another owner's bookings.

Employee:
- Can only see bookings where Booking.EmployeeId matches their own Employee record.
- Must never access another employee's bookings, even if they manually supply another employeeId.
- Must not be able to use filters to bypass their data scope.

Customer:
- Do not grant access to this endpoint unless an existing explicit authorization convention requires it.
- Anonymous users must not access it.

Resolve the authenticated user's identity using existing project conventions. Do not trust user IDs or Business IDs supplied by the client for authorization.

## 4. Filtering behavior

Apply filters server-side using IQueryable before pagination.

- status: exact status match.
- employeeId: filter by employee.
- customerId: filter by customer.
- serviceId: filter by service.
- fromDate: inclusive lower boundary for StartTime.
- toDate: inclusive upper boundary for StartTime.
- search: case-insensitive search against the customer's name and phone number.

Important:
- Inspect the project's DateTime and UTC conventions before implementing date filters.
- Do not assume query dates are UTC or business-local without checking existing API conventions.
- If the project already has a business timezone handling approach, reuse it.
- Validate date ranges; fromDate must not be later than toDate.
- Reject invalid or undefined enum values.
- Empty search values should not add unnecessary filters.
- Do not load all bookings into memory to filter them.

For Business Owners, employeeId can narrow results within their authorized business scope.

For Employees, ignore or reject an employeeId that does not match their own employee record. Never return other employees' bookings.

## 5. Pagination and sorting

Defaults:
- page = 1
- pageSize = 20
- maximum pageSize = 100

Requirements:
- Validate page >= 1.
- Validate pageSize between 1 and 100.
- Apply filters before Count and pagination.
- Use database-side Skip and Take.
- Sort by StartTime descending by default.
- Add Id as a secondary sort key for deterministic ordering.
- Avoid loading unnecessary navigation properties.
- Use AsNoTracking for read-only queries where appropriate.
- Project directly into a response DTO instead of returning EF entities.

Return pagination metadata:
- items
- page
- pageSize
- totalCount
- totalPages
- hasPreviousPage
- hasNextPage

Follow existing response conventions if a pagination response model already exists.

## 6. Response DTO

Return only fields needed by the booking management UI.

At minimum, inspect whether the following fields are appropriate and available:
- bookingId
- startTime
- endTime
- status
- creatorType
- customer name and phone
- service name and duration
- employee name
- businessId

Do not expose sensitive user information, internal navigation objects, or unnecessary database fields.

Use existing DTO naming and serialization conventions.

## 7. Performance

- Keep filtering, searching, counting, ordering, and pagination in the database query.
- Avoid N+1 queries.
- Avoid unnecessary Include calls if projection can retrieve the required fields.
- Do not introduce caching or complex search infrastructure.
- Inspect generated queries if necessary to ensure filters are translated to SQL.

## 8. Tests

Add tests for:

1. Owner can list bookings belonging to their business.
2. Owner cannot see another business's bookings.
3. Employee sees only their own bookings.
4. Employee cannot access another employee's bookings by supplying employeeId.
5. Anonymous access is rejected.
6. Customer access is rejected unless explicitly supported by existing conventions.
7. Filtering by status.
8. Filtering by employeeId.
9. Filtering by customerId.
10. Filtering by serviceId.
11. Filtering by fromDate.
12. Filtering by toDate.
13. Search by customer name.
14. Search by customer phone.
15. Combined filters.
16. Default pagination values.
17. Custom page and pageSize.
18. pageSize greater than 100 is rejected.
19. Invalid page values are rejected.
20. fromDate later than toDate is rejected.
21. Empty result returns valid empty pagination metadata.
22. Stable sorting when multiple bookings have the same StartTime.

Use the existing test framework and PostgreSQL integration tests where appropriate.

## 9. Scope restrictions

Do NOT implement:
- Customer booking history endpoint.
- Booking creation or status modification changes.
- Booking deletion.
- Rescheduling.
- Notifications.
- Frontend changes.
- Dashboard statistics.
- New search infrastructure.
- Changes to the legacy Appointment entity.

Do not apply migrations.

## 10. Final report

Report:
- Endpoint and query parameter examples.
- Authorization and data isolation behavior.
- DTO and pagination response shape.
- Filtering and sorting behavior.
- Files created or modified.
- Tests executed and results.
- Whether a migration is required.
- Any unresolved limitations.

Do not claim tests passed unless they were actually executed.