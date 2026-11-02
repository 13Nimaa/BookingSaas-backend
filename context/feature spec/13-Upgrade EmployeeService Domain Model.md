# Phase 1 — Upgrade EmployeeService Domain Model

Implement the domain and database foundation for employee-specific service pricing and duration.

## 1. Objective

Each employee must be able to have custom pricing and duration for the services assigned to them.

The original Service entity must remain the source of default pricing and duration.

An employee's custom values must be optional.

## 2. EmployeeService Model

Extend the existing EmployeeService relationship/entity to support:

- `PriceOverride`: nullable, using the existing project monetary type.
- `DurationMinutesOverride`: nullable integer.

Do not create a second relationship or duplicate the existing Employee-Service association.

Preserve all existing properties, relationships, and conventions.

## 3. Default Value Behavior

The following rules must apply:

- If `PriceOverride` is null, use the original Service price.
- If `DurationMinutesOverride` is null, use the original Service duration.
- If an override has a value, it takes precedence over the original Service value.
- Null means "inherit the Service default", not zero.

Do not copy the original Service values into the override columns when creating an EmployeeService record.

This is intentional: employees without overrides must automatically benefit from future changes to the Service defaults.

## 4. Entity Configuration

Update the existing EF Core configuration accordingly.

Requirements:

- Configure both override properties correctly.
- Preserve the existing Employee and Service foreign keys.
- Preserve the unique constraint/index preventing duplicate Employee-Service assignments.
- Preserve existing delete behaviors and ownership relationships.
- Follow existing database naming and configuration conventions.

Do not introduce unnecessary abstractions.

## 5. Database Migration

Create an EF Core migration that adds the two nullable columns to the existing EmployeeService table.

Migration requirements:

- Existing EmployeeService records must remain intact.
- Existing assignments must receive null override values.
- No existing Service or Employee data should be modified.
- No existing booking records should be modified.
- Do not drop or recreate the EmployeeService table.
- Preserve existing indexes and constraints.

**Important: Create the migration but DO NOT apply it to the database.** I will review and approve migration application separately.

## 6. Backward Compatibility

Existing Employee-Service assignments must continue to work without requiring manual data updates.

Existing GET, POST, and DELETE functionality must not break because of this domain change.

Do not change existing API response contracts in this phase unless a minimal adjustment is strictly required for compilation.

## 7. Scope Restrictions

Do NOT implement the following in this phase:

- Employee-Service PUT endpoint.
- Changes to existing GET/POST/DELETE API behavior.
- Availability calculations.
- Booking creation or booking duration calculations.
- Booking price snapshots.
- Frontend changes.
- Unrelated refactoring.

These will be implemented in subsequent phases.

## 8. Tests

Add or update focused tests to verify:

1. EmployeeService can be created without overrides.
2. EmployeeService can store a custom price.
3. EmployeeService can store a custom duration.
4. Both overrides can be null independently.
5. Existing Employee-Service assignment behavior remains intact.
6. Duplicate Employee-Service assignments remain prevented.

## 9. Completion Report

After implementation, report:

- Files created or modified.
- Changes made to EmployeeService.
- EF Core configuration changes.
- Migration name and generated schema changes.
- Tests executed and their results.
- Any architectural issue that prevents completion.

Do not apply the migration.

Do not proceed to Phase 2.