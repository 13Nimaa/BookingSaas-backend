# BookingSaaS — Phase 1: Booking Domain Foundation

Implement Phase 1 of the agreed BookingSaaS v1 architecture in the existing ASP.NET Core backend.

Project: `E:\BookingSaas-backend`

The architecture audit has already been completed. Do not repeat the audit or spend time generating another report. Use the existing implementation and audit findings as your baseline.

## Important Rules

- Work only on Phase 1.
- Do not implement BookingController or booking endpoints.
- Do not implement availability.
- Do not implement BackgroundService or expiration scheduling yet.
- Do not implement Employee-Service API yet.
- Do not modify or delete Appointment.
- Do not create a second booking entity.
- Do not refactor unrelated parts of the project.
- Preserve existing naming conventions and architecture.
- Inspect the exact existing definitions before editing them.
- If a required relationship or type differs from the audit description, stop and ask instead of guessing.
- Do not add tests unless explicitly requested.

## 1. BookingStatus

File: `Domain/Entities/Booking.cs` or the existing location of BookingStatus.

Add:

`Expired`

Preserve all existing status values and their numeric values. Do not reorder existing enum members, because Status is currently stored as an integer in PostgreSQL.

## 2. CreatorType

Introduce a `BookingCreatorType` enum with:

- Customer
- Guest
- BusinessOwner

Follow the existing enum organization and naming conventions.

Add the following properties to Booking:

- CreatorType
- CreatedByUserId (nullable string)
- Navigation property to ApplicationUser only if consistent with the existing entity conventions.

Rules:

- Registered Customer: CreatorType = Customer; CreatedByUserId = authenticated user's ID.
- Guest: CreatorType = Guest; CreatedByUserId = null.
- BusinessOwner: CreatorType = BusinessOwner; CreatedByUserId = authenticated owner's ID.

Do not implement creator assignment in booking services yet. This phase only establishes the domain model.

Ensure the optional ApplicationUser relationship does not cause unintended cascade deletion of bookings.

## 3. Business TimeZoneId

Add a `TimeZoneId` property to Business.

Requirements:

- Use string.
- Make it required if the existing project conventions and current business creation flow allow a safe default.
- Choose a sensible default only if it is compatible with the project's existing supported business locations.
- Do not hardcode a timezone that silently misrepresents businesses in other countries.
- If a safe default cannot be determined, make the property nullable for now and explain the follow-up requirement.

Do not modify unrelated Business behavior.

## 4. BookingConfiguration

Create `BookingConfiguration` using the project's existing EF Core configuration conventions.

Configure:

- Booking primary key.
- Required Business relationship.
- Required Customer relationship.
- Required Employee relationship.
- Required Service relationship.
- Optional CreatedByUser relationship.
- Appropriate delete behavior to preserve booking history.

Important:

- Do not blindly change existing cascade behavior.
- Inspect the existing relationships and migration snapshot.
- Prevent deleting Customer, Employee or Service records that are referenced by bookings.
- Do not introduce an exclusion constraint or concurrency mechanism in this phase.
- Do not configure relationships that already conflict with the current model.

Add only appropriate indexes that are useful for booking queries, especially employee and time-based lookups.

Register the configuration through the existing DbContext configuration mechanism.

## 5. Database Migration

After the code changes:

- Create one EF Core migration for this phase.
- Do not apply the migration to any database.
- Do not run `database update`.
- Inspect the generated migration for unintended table drops, column drops, or destructive operations.
- Preserve existing data and existing enum integer values.
- If the migration contains destructive or unexpected operations, stop and report them instead of attempting to fix them blindly.

## 6. Verification

Run the project's normal build/type-check command.

Do not run database migrations.

Do not modify Docker containers or database contents.

## 7. Final Report

Report:

1. Files changed.
2. New enum and properties.
3. Relationship and delete behavior changes.
4. Business timezone handling.
5. Migration filename and summary.
6. Build result.
7. Any unresolved issues.

Do not continue to Phase 2.

Stop and wait for approval.