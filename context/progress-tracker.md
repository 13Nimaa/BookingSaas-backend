# Backend progress

**Last updated:** 2026-11-01  
**Current task:** Phase 1 — Booking domain foundation (`feature-spec/02-booking-foundation.md`) — **In progress**  
**Prior:** Business integration tests — complete  
**Docs:** Agent context suite added (`AGENTS.md`, overview, architecture, standards, workflow, issues)

---

# Agent context suite

| Area | Status |
|------|--------|
| Root + `context/AGENTS.md` | Reading order for agents | Done |
| `project-overview.md` | Product goals / scope | Done |
| `architecture-context.md` | Layers + invariants | Done |
| `code-standards.md` | Backend conventions | Done |
| `ai-workflow-rules.md` | Scoping + doc sync | Done |
| `current-issues.md` | Open debt / findings | Done |
| `progress-tracker.md` | This file (frontend-style) | Done |

Existing docs kept: `PROJECT_INVENTORY.md`, `feature-spec/*`, API frontend notes, test notes.

---

# Phase 1 — Booking domain foundation

Spec: `context/feature-spec/02-booking-foundation.md`  
**Do not start Phase 2 until this phase is approved.**

| Area | Status |
|------|--------|
| `BookingStatus.Expired` appended (preserve int values) | Done (code) |
| `BookingCreatorType` + `CreatorType` / `CreatedByUserId` / `CreatedByUser` | Done (code) |
| `Business.TimeZoneId` required, default `Asia/Tehran` | Done (code) |
| `BookingConfiguration` (Restrict on Business/Customer/Employee/Service; SetNull on CreatedByUser; employee/time indexes) | Done (code) |
| Auto-register via `ApplyConfigurationsFromAssembly` | Done (existing mechanism) |
| EF migration created (not applied) | Pending |
| `dotnet build` verification | Pending |
| BookingController / availability / BackgroundService / creator assignment in services | Out of scope |

## Files (this phase so far)

| Path | Role |
|------|------|
| `Domain/Entities/Booking.cs` | Status + creator enum/props |
| `Domain/Entities/Business.cs` | `TimeZoneId` |
| `Infrastructure/Persistence/Configurations/BusinessConfiguration.cs` | Required timezone + default |
| `Infrastructure/Persistence/Configurations/BookingConfiguration.cs` | Relationships, deletes, indexes |

## Verify (when resuming Phase 1)

```bash
dotnet ef migrations add BookingDomainFoundation --output-dir Migrations
# Inspect migration — do NOT database update
dotnet build
```

## Timezone decision

Project is Iran-focused (Iranian phone conventions, Jalali on frontend, sample cities like Mashhad). `TimeZoneId` is required with default / DB default `Asia/Tehran`. Multi-country support later should accept timezone on create/update DTOs.

---

# Business integration tests — previously complete

| Area | Status |
|------|--------|
| Auth fixture reuse + FakeImageUploadService | Done |
| Business create / retrieve / update / delete / authz tests | Done |
| Production business code unchanged | Done |

## Test run (historical)

```bash
dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --verbosity minimal
```

Result recorded: **30 total — 30 passed** (Auth 6, Business 24). Docker required.

Findings from that pass live in `current-issues.md` (Business API + Auth sections).

---

# Customer CRM (`feature-spec/01-complete-customers.md`) — previously complete

CRM fields, validators, list/query, soft-active behavior — see inventory. Spec folder retained for history.

---

# Next (after Phase 1 approval)

1. Finish Phase 1 migration + build report.
2. Later phases per booking architecture: DI + controller, availability, expiration scheduling, creator assignment — only when specified.
