# Development Workflow

## Approach

Build this backend incrementally against context files and feature specs. Context defines what exists, what is missing, and how to implement. Do not invent product behavior or API contracts — check `PROJECT_INVENTORY.md` and the active `feature-spec` first.

## Scoping Rules

- Work on one feature unit / phase at a time (e.g. booking domain foundation, not foundation + controller + availability).
- Prefer small, verifiable increments.
- Do not combine unrelated system boundaries in one change (auth hardening + booking schema, etc.).
- Obey explicit “do not implement” / “do not modify” rules inside the active feature spec.

## When To Split Work

Split if a step combines:

- Schema/migration work and a full HTTP controller surface
- Multiple unrelated domains (customers + bookings + employees)
- Destructive data changes and feature delivery
- Test infrastructure overhaul and product features

If the change cannot be verified quickly (`dotnet build` or focused tests when requested), the scope is too broad.

## Handling Missing Requirements

- Do not invent endpoints, DTOs, or lifecycle rules that are not in inventory or the active spec.
- If a requirement is ambiguous, or live code conflicts with the spec/audit, stop and ask — record the blocker in `progress-tracker.md` / `current-issues.md`.
- If blocked on a later phase dependency, implement only the allowed slice.

## Protected Foundation

Avoid modifying unless the task requires it:

- Identity / JWT wiring in `Program.cs`
- Legacy `Appointment` entity and its configuration
- Unrelated controllers and DTOs
- Applied database state / Docker containers

## Keeping Docs In Sync

Update the relevant context file when implementation changes:

| Change | Update |
| ------ | ------ |
| Product scope / flows | `project-overview.md` |
| Boundaries / invariants | `architecture-context.md` |
| Conventions | `code-standards.md` |
| Wired vs missing APIs / entities | `PROJECT_INVENTORY.md` |
| Known bugs / debt | `current-issues.md` |
| Progress / open questions | `progress-tracker.md` |

Progress must reflect actual implementation state, not intent.

## Migrations

1. Make model + configuration changes first.
2. Create one EF migration for the unit of work.
3. Inspect the generated migration for unintended drops or destructive ops.
4. Do **not** apply the migration unless explicitly asked.
5. Note migration name + summary in `progress-tracker.md`.

## Before Moving To The Next Unit

1. Current unit matches the feature-spec scope (nothing extra).
2. No invariant in `architecture-context.md` was violated.
3. `dotnet build` (or the project’s normal check) succeeds when verification is required.
4. `progress-tracker.md` reflects completed work and remaining blockers.
5. Inventory / issues updated if status changed.
