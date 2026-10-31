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

1. **`IBookingService` not registered in DI** — booking HTTP cannot run yet.
2. **No `BookingController`** — by design until a later phase.
3. **`BookingDto.cs` empty** — response contract undefined.
4. **Dual models:** `Booking` (canonical going forward) vs unused `Appointment` — do not delete Appointment without an explicit migrate plan.
5. **Phase 1 domain foundation in progress:** `Expired` status, `BookingCreatorType`, `CreatedByUserId`, `Business.TimeZoneId`, `BookingConfiguration` added in code; EF migration + build verification still outstanding.
6. Creator assignment is **not** implemented in `BookingService` yet (Phase 1 domain-only).

## Roles dualism

- `ApplicationUser.Role` enum `{ Admin, Staff, Customer }` is unused by controllers.
- Identity roles `{ Customer, BusinessOwner, Employee, Admin }` are authoritative.

## Test / infra

- BusinessController depends on `IImageUploadService`; tests stub Cloudinary via `FakeImageUploadService` in the auth fixture.
- Integration tests require Docker Desktop (Testcontainers). A running API process locking `bin/Debug` can block rebuilds.
