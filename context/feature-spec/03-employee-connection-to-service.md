# Phase 2 — Employee-Service Management API

**Project:** `E:\BookingSaas-backend`

## Objective

Implement a complete, production-conscious API for managing the relationship between employees and services within a business.

The project already contains an `EmployeeService` join entity/table and the related `Employee` and `Service` entities. Reuse the existing domain model and project conventions.

Do not redesign the existing architecture.

## 1. Start by Understanding the Existing Implementation

Inspect only the relevant existing code:
- `EmployeeService` entity and its configuration.
- `Employee` and `Service` entities and their relationships.
- `Business` entity and ownership relationship.
- Existing DbContext configuration.
- Existing Employee and Service controllers, services, DTOs, validators, and authorization policies.
- Existing API response and error-handling conventions.

Before implementation, establish:
- How an employee is associated with a business.
- How a service is associated with a business.
- How business ownership is verified in existing endpoints.
- Whether EmployeeService already has a unique composite index.
- Whether the join entity has additional properties beyond EmployeeId and ServiceId.

Do not assume property names or invent missing relationships. Follow the actual codebase.

## 2. Required API Operations

Implement the following operations:

### A. Get Services Assigned to an Employee

`GET /api/employees/{employeeId}/services`

Requirements:
- Return services assigned to the specified employee.
- Include only services belonging to the same business.
- Verify the requesting BusinessOwner owns that business.
- Return a consistent DTO, not EF entities.
- Handle nonexistent employees and unauthorized access correctly.

### B. Assign a Service to an Employee

`POST /api/employees/{employeeId}/services`

Request body should contain the service ID.

Requirements:
- Verify the requesting BusinessOwner owns the employee's business.
- Verify the employee exists.
- Verify the service exists.
- Verify the service belongs to the same business as the employee.
- Prevent duplicate assignments.
- Return an appropriate success response.
- Handle concurrent duplicate requests safely where practical.

### C. Remove a Service from an Employee

`DELETE /api/employees/{employeeId}/services/{serviceId}`

Requirements:
- Verify business ownership.
- Verify the employee and service belong to the same business.
- Return an appropriate response if the assignment does not exist.
- Do not allow removal if existing business rules or booking-history constraints prohibit it.
- Do not delete the Employee or Service themselves.

## 3. Authorization and Security

- Only authenticated BusinessOwners who own the relevant business can manage employee-service assignments.
- Do not trust BusinessId supplied by the client.
- Derive business ownership from the authenticated user and existing database relationships.
- Do not allow one business owner to modify another business's employees or services.
- Follow the project's existing authorization conventions.

## 4. Validation and Error Handling

- Use the project's existing validation approach.
- Validate IDs and request payloads.
- Use the existing global exception handling and API response conventions.
- Return clear, consistent error messages in Persian, matching the existing API.
- Avoid leaking internal exception details.

## 5. Database and EF Core

- Reuse the existing EmployeeService table.
- Ensure duplicate assignments cannot be persisted.
- If a unique composite index is missing, add one through EF Core configuration and create a migration.
- Create a migration only if actual schema changes are needed.
- Do NOT apply any migration to the database.
- Do not modify unrelated tables or relationships.
- Do not change Appointment or Booking behavior in this phase.

## 6. Important Booking Compatibility

This API will be used by the future Booking validation flow.

Therefore:
- A booking must eventually be accepted only when the selected employee is assigned to the selected service.
- Keep the EmployeeService relationship easy to query efficiently.
- Do not implement booking validation in this phase.
- Do not change BookingService yet.

## 7. Tests

Add focused tests if the repository already has an established test project and testing conventions.

At minimum, cover:
- Successful assignment.
- Duplicate assignment.
- Assignment of a service from another business.
- Unauthorized owner access.
- Successful removal.
- Removal of a nonexistent assignment.

Do not introduce a new testing framework solely for this phase.

## 8. Strict Scope

Do NOT implement:
- Booking API or BookingController.
- Booking availability or scheduling.
- Timezone calculations.
- Booking expiration BackgroundService.
- Booking status transitions.
- Appointment modifications.
- Frontend changes.
- Unrelated refactoring.

## 9. Final Report

When finished, report:
1. Files created or modified.
2. Endpoints and request/response DTOs.
3. Authorization and business-ownership checks.
4. Validation and duplicate-prevention strategy.
5. Database/index/migration changes, if any.
6. Build and test results.
7. Any unresolved issues or assumptions.

Do not apply migrations.

Stop after Phase 2 is complete. Do not proceed to Booking implementation automatically.