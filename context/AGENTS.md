# BookingSaaS Backend — Agent Rules

## Application Building Context

Read the following files in order before implementing or making any architectural decision:

1. `context/project-overview.md` — product definition, goals, features, and scope
2. `context/architecture-context.md` — system structure, boundaries, and invariants
3. `context/code-standards.md` — implementation rules and conventions
4. `context/ai-workflow-rules.md` — development workflow, scoping rules, and delivery approach
5. `context/progress-tracker.md` — current phase, completed work, open questions, and next steps
6. `context/current-issues.md` — known bugs, debt, and findings not silently fixed
7. `context/PROJECT_INVENTORY.md` — wired vs missing APIs, entities, and BE gaps

Frontend contracts (for Next.js / API consumers):

- `context/Booking-MVP-frontend.md` — specs 01–11 booking MVP implementation guide
- `context/Customer-API-frontend.md` — customer CRM detail
- `context/WorkingHour-API-frontend.md` — date-based working hours
- `context/Business-List-API-frontend.md` — public business list search / city / sort / page

When a task points at a feature spec under `context/feature-spec/`, read that spec and follow its scope rules exactly.

Update `context/progress-tracker.md` after each meaningful implementation change.

If implementation changes architecture, conventions, inventory status, or known issues, update the relevant context file before continuing.
