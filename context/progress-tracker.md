# Progress tracker — 2026-10-29

## Current status: Business integration tests complete

- Implemented Business module integration tests in `tests/BookingSaas-backend.Tests/BussinessTests.cs`.
- Reused existing `AuthFixture` / `AuthCollection` (Testcontainers PostgreSQL + `WebApplicationFactory`).
- Shared fixture updated with a `FakeImageUploadService` so BusinessController can be constructed without Cloudinary credentials.
- Production code was not changed.

## Test run

- Command: `dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --verbosity minimal`
- Prerequisite: Docker Desktop running (Testcontainers). A running `BookingSaas-backend` process locking `bin/Debug` can block builds; stop it before rebuild if needed.
- **Result: 30 total — 30 passed, 0 failed, 0 skipped**
  - Auth: 6 passed
  - Business: 24 passed

## Business coverage implemented

### Creation
- Authenticated customer can self-register a business via `POST /api/Business/register`, receives new tokens, gains `BusinessOwner`, and owns the created row.
- Unauthenticated register → `401`.
- Customer cannot use Admin-only `POST /api/Business` → `403`.
- Null required fields → `400`.
- Duplicate register for same owner → `409`.
- Admin can create via `POST /api/Business` → `201`, `OwnerId` = admin user id.

### Retrieval
- Owner retrieves own business via `GET /api/Business/mine`.
- `mine` does not return another owner’s business.
- Public `GET /api/Business` and `GET /api/Business/{id}` work without auth.
- Missing id → `404`; unauthenticated `mine` → `401`.

### Update
- Owner can update permitted fields via `PUT /api/Business/{id}`.
- Other owner / Customer → `403`.
- Null required fields → `400`.
- Extra payload fields (`ownerId`, `id`) cannot change ownership or entity id.
- Nonexistent id update does not succeed (returns `403` with current implementation; see findings).

### Deletion
- **Exists:** `DELETE /api/Business/{id}` is Admin-only → `204`.
- Owner / Customer → `403`; unauthenticated → `401`; missing id → `404`.

### Authorization / security
- URL business-id swap cannot bypass ownership on update.
- Forbidden responses do not expose exception / stack / Npgsql details.

## Findings (not silently fixed)

1. **Update missing business returns `Forbid` instead of `NotFound`.**  
   `BusinessController.Update` (`Controllers/BusinessController.cs` ~166–173) compares `userId != business?.OwnerId` before the null check. When the row is missing, `business?.OwnerId` is null and a non-null owner gets `403`. Prefer null-check first, then ownership, then update.

2. **No DTO validation beyond nullability.**  
   `CreateBusinessDto` has no `[Required]` / length attributes. Empty strings can pass API model binding; only JSON `null` reliably yields `400`. DB max lengths exist in `BusinessConfiguration` but are not mirrored on the DTO.

3. **`UpdateBusinessDto` is unused.**  
   Update accepts `CreateBusinessDto`. Harmless but confusing for API consumers.

4. **Admin create does not assign `BusinessOwner`.**  
   `POST /api/Business` sets `OwnerId` to the Admin’s id only. That admin cannot call owner-only `PUT` unless also given `BusinessOwner` (or a separate Admin update path is added).

5. **Test infrastructure required a Cloudinary stub.**  
   `BusinessController` always resolves `IImageUploadService` → `CloudinaryImageUploadService`, which throws when Cloudinary settings are empty. Fixture now replaces it with `FakeImageUploadService` in `AuthTests.cs`.

## Prior auth audit notes (still open; unchanged this pass)

1. JWT signing key missing from checked-in config (`Jwt:Key`).
2. Refresh tokens returned in JSON as well as HttpOnly cookie.
3. Weak/missing request validation on auth DTOs.
4. Registration ignores role create/assign failure results.
5. Business registration elevates role but old access tokens stay valid until client switches to the new token.

## Files changed this pass

- `tests/BookingSaas-backend.Tests/BussinessTests.cs` — Business integration tests (new).
- `tests/BookingSaas-backend.Tests/AuthTests.cs` — register `FakeImageUploadService` in fixture.
- `context/progress-tracker.md` — this update.
