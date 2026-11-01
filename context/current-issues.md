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

1. JWT signing key is not in checked-in config (`Jwt:Key` via User Secrets / env).
2. Refresh tokens returned in JSON as well as HttpOnly cookie.
3. Weak/missing request validation on some auth DTOs.
4. Registration ignores role create/assign failure results in places.
5. Business registration elevates role; old access tokens stay valid until the client switches to the new token.

## Booking

1. **Dual models:** `Booking` (canonical going forward) vs unused `Appointment` — do not delete Appointment without an explicit migrate plan.
2. **Phase 1 domain foundation migration not applied:** `20261031213454_BookingDomainFoundation` (creator fields, `TimeZoneId`, Restrict FKs, indexes) exists in repo but is **not applied** to local/Docker DB outside Testcontainers.
3. **Phase 5 exclusion migration not applied:** `20261101201947_BookingActiveOverlapExclusion` (`btree_gist` + active-booking overlap exclude) exists in repo; Testcontainers apply it via `MigrateAsync`. Local/Docker app DBs need explicit `database update`.
4. **Availability is informational only** — `GET /api/bookings/availability` does not reserve slots; create enforces overlap via DB exclusion (after Phase 5 migration is applied).
5. **No owner-create-on-behalf endpoint** yet (`GET /api/bookings/mine` customer history is live; owner/employee list + detail exist).
6. ~~`IBookingService` DI / `BookingController` / empty `BookingDto` / creator assignment~~ — Done in Phase 3.
7. ~~Transactional overlap on create~~ — Done in Phase 5 (Postgres EXCLUDE).
8. ~~Pending auto-expiration BackgroundService~~ — Done in Phase 8.

## Roles dualism

- `ApplicationUser.Role` enum `{ Admin, Staff, Customer }` is unused by controllers.
- Identity roles `{ Customer, BusinessOwner, Employee, Admin }` are authoritative.

## Test / infra

- BusinessController depends on `IImageUploadService`; tests stub Cloudinary via `FakeImageUploadService` in the auth fixture.
- Integration tests require Docker Desktop (Testcontainers). A running API process locking `bin/Debug` can block rebuilds.
