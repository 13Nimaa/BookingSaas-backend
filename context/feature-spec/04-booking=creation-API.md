# Phase 3 — Booking Creation API

**Project:** `E:\BookingSaas-backend`

**Context:** Phase 1 (Booking Domain Foundation) and Phase 2 (Employee-Service Management API) are complete.

Read the existing `AGENTS.md` and relevant context documentation before implementation. Follow the existing architecture, naming conventions, authorization patterns, validation strategy, and API response conventions.

## Objective

Implement the first functional Booking Creation API for:
1. Authenticated customers.
2. Guest customers.

The goal is to create valid Pending bookings while enforcing business, employee, service, customer, and creator consistency.

## 1. Inspect Relevant Existing Code

Inspect only the code needed for this feature:
- Booking and BookingStatus entities.
- BookingService and IBookingService.
- CustomerService and customer creation/retrieval methods.
- Employee, Service, Business, and EmployeeService relationships.
- Existing Booking DTOs.
- Authentication and authorization conventions.
- EmployeeServicesController implementation.
- EF Core BookingConfiguration.
- Existing integration test infrastructure.

Reuse existing implementations. Do not redesign the architecture or duplicate existing customer-management logic.

## 2. API Endpoints

Implement:

### Authenticated customer booking

`POST /api/bookings`

The authenticated user's identity must determine the customer. Do not accept CustomerId or UserId from the request body.

### Guest booking

`POST /api/bookings/guest`

Accept guest name and phone number along with the booking selection.

Guest bookings must not automatically connect to an existing registered customer's account based only on matching phone numbers.

Both endpoints must use the same core booking validation and creation logic wherever practical.

## 3. Booking Validation

Perform all validation on the backend.

Before creating a booking, verify:

- Business exists and is active according to existing project rules.
- Service exists and belongs to the selected business.
- Employee exists and belongs to the selected business.
- Employee is assigned to the selected service through EmployeeService.
- StartTime is valid.
- Required customer information is valid.
- The authenticated user is permitted to use the authenticated-customer endpoint.

Do not trust client-provided EndTime.

Calculate EndTime using:

`StartTime + Service.DurationMinutes`

Use the project's existing DateTime/UTC conventions. Do not introduce arbitrary timezone conversions in this phase.

## 4. Creator Tracking

Set creator information server-side:

| Request type | CreatorType | CreatedByUserId |
|---|---|---|
| Authenticated customer | Customer | Authenticated user ID |
| Guest | Guest | null |

Do not implement BusinessOwner booking creation in this phase.

## 5. Booking Creation

- New bookings must start with `BookingStatus.Pending`.
- Use existing CustomerService methods to get or create the customer.
- Preserve business-scoped customer behavior.
- Do not create duplicate customers unnecessarily.
- Persist the booking through the existing service/repository conventions.
- Return a consistent Booking DTO.
- Do not expose EF entities directly.

## 6. Dependency Injection

Register `IBookingService` in the existing DI registration location.

Use the project's existing lifetime conventions.

## 7. Validation and Error Handling

- Use FluentValidation if that is the existing project convention.
- Return Persian validation and error messages consistent with existing APIs.
- Handle nonexistent resources, invalid relationships, and unauthorized requests consistently.
- Avoid leaking internal exception details.

## 8. Important Scope Boundaries

Do NOT implement:
- Availability API.
- Working-hours validation.
- Overlapping-booking prevention or PostgreSQL exclusion constraints.
- Booking expiration BackgroundService.
- Owner approval or status-transition endpoints.
- BusinessOwner booking creation.
- OTP or phone verification.
- Appointment changes.
- Frontend changes.
- Unrelated refactoring.

Do not claim that time-slot availability is guaranteed. Availability and concurrency protection belong to later phases.

## 9. Database

- Reuse the existing Booking schema and Phase 1 migration.
- Do not apply migrations.
- Do not modify the database schema unless genuinely required.
- If a schema change is necessary, create a migration but do not apply it and explain why.

## 10. Tests

Add focused integration tests following existing test conventions.

Cover at least:
1. Authenticated customer creates a valid booking.
2. Guest creates a valid booking.
3. Client-provided EndTime cannot override the calculated duration.
4. Employee from another business is rejected.
5. Service from another business is rejected.
6. Employee not assigned to the selected service is rejected.
7. Unauthenticated access to the authenticated endpoint is rejected.
8. CreatorType and CreatedByUserId are persisted correctly.
9. Guest with a phone matching an existing registered customer is not linked to that user's account.

Do not introduce a new testing framework.

## 11. Final Report

Report:
- Files created/modified.
- Endpoints and DTOs.
- Validation rules.
- Creator tracking implementation.
- Dependency injection registration.
- Integration test results.
- Build result.
- Migration details, if any.
- Unresolved issues.

Do not start another phase automatically.

Stop after Phase 3.