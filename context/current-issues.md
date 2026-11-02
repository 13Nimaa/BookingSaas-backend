# Current issues & debt

Tracked findings that should not be silently “fixed” during unrelated work. Resolve only when the active task covers them.

## Business API

1. **Update missing business returns `Forbid` instead of `NotFound`.**  
   `BusinessController.Update` checks ownership before null. Prefer null-check → ownership → update.

2. **Weak DTO validation on business create/update.**  
   Empty strings can pass model binding; DB max lengths are not fully mirrored on DTOs.

3. **`UpdateBusinessDto` unused.**  
   Update accepts `CreateBusinessDto`.

4. **Admin create does not assign `BusinessOwner`.**  
   `POST /api/Business` sets `OwnerId` to the admin but does not grant the owner role path used by owner-only endpoints.

## Auth

1. JWT signing key is not in checked-in config (`Jwt:Key` via User Secrets / env). **MVP deploy blocker** if unset.
2. Refresh tokens returned in JSON as well as HttpOnly cookie.
3. Weak/missing request validation on some auth DTOs.
4. Registration ignores role create/assign failure results in places.
5. Business registration elevates role; old access tokens stay valid until the client switches to the new token.

## Production / config (Phase 11)

1. **Committed DB credentials** in `appsettings.json` — treat as leaked for any shared/prod use; use env / secret store; do not ship as-is.
2. **No `appsettings.Production.json` / `.env.example`** — prod values (connection, JWT key, CORS origins, Cloudinary) under-documented.
3. **`Frontend:Origins` is `http://localhost:4000`** while some docs still mention `3000`.
4. **Cloudinary** empty in repo — image upload fails until configured.
5. **`PendingBookingExpirationService` has no single-leader guard** — N replicas ⇒ N pollers (idempotent, extra load).
6. **Package advisory NU1903** — `Microsoft.OpenApi` 2.0.0 (build warning).
7. **Postgres deploy:** role must allow `btree_gist` + EXCLUDE (`20261101201947_BookingActiveOverlapExclusion`).

## Booking

1. **Dual models:** `Booking` (canonical going forward) vs unused `Appointment` — do not delete Appointment without an explicit migrate plan.
2. **Phase 1 domain foundation migration not applied:** `20261031213454_BookingDomainFoundation` (creator fields, `TimeZoneId`, Restrict FKs, indexes) exists in repo but is **not applied** to local/Docker DB outside Testcontainers.
3. **Phase 5 exclusion migration not applied:** `20261101201947_BookingActiveOverlapExclusion` (`btree_gist` + active-booking overlap exclude) exists in repo; Testcontainers apply it via `MigrateAsync`. Local/Docker app DBs need explicit `database update`.
3b. **EmployeeService overrides migration not applied:** `20261102103531_EmployeeServiceOverrides` (`PriceOverride`, `DurationMinutesOverride`) exists in repo; Testcontainers apply via `MigrateAsync`. Availability/booking still use `Service` defaults only until later phases resolve overrides.
4. **Availability is informational only** — `GET /api/bookings/availability` does not reserve slots; create enforces overlap via DB exclusion (after Phase 5 migration is applied).
5. **No owner-create-on-behalf endpoint** yet (`GET /api/bookings/mine` customer history is live; owner/employee list + detail exist).
6. **Create DTOs expose `EndTime` but service ignores it** — `EndTime` is always `StartTime + DurationMinutes`; clients can send a mismatched value with no validation (contract footgun).
7. **Inconsistent error bodies** — booking business errors often return plain string; FluentValidation returns ProblemDetails-style validation errors.
8. **`CreatedAtAction` on create** points at create action names, not `GET api/bookings/{id}` — weak `Location` header.
9. ~~`IBookingService` DI / `BookingController` / empty `BookingDto` / creator assignment~~ — Done in Phase 3.
10. ~~Transactional overlap on create~~ — Done in Phase 5 (Postgres EXCLUDE).
11. ~~Pending auto-expiration BackgroundService~~ — Done in Phase 8.

## Roles dualism

- `ApplicationUser.Role` enum `{ Admin, Staff, Customer }` is unused by controllers.
- Identity roles `{ Customer, BusinessOwner, Employee, Admin }` are authoritative.

## Test / infra

- BusinessController depends on `IImageUploadService`; tests stub Cloudinary via `FakeImageUploadService` in the auth fixture.
- Integration tests require Docker Desktop (Testcontainers). A running API process locking `bin/Debug` can block rebuilds.
