# Authentication audit — 2026-10-29

## Status

- Reviewed the authentication implementation and existing integration tests.
- Ran the existing auth test project without changing production code or tests.
- Test command: `dotnet test tests/BookingSaas-backend.Tests/BookingSaas-backend.Tests.csproj --no-restore --verbosity normal`
- Result: 6 discovered, 0 passed, 6 failed. All failures were infrastructure failures before the HTTP tests ran: Testcontainers could not connect to Docker at `npipe://./pipe/docker_engine`.
- No test scenarios were confirmed by execution because the PostgreSQL test container never started.

## Confirmed implementation bugs / fixes needed

1. JWT startup has no configured signing key in the checked-in configuration. `Program.cs:50` reads `Jwt:Key`, and `Program.cs:67` immediately passes it to `Encoding.UTF8.GetBytes`; `appsettings.json` contains issuer and audience but no `Jwt:Key`. Unless an external secret provider supplies it, the application cannot start safely. Add a required test/production secret configuration and fail with a clear configuration error when absent.

2. Raw refresh tokens are returned in JSON responses as well as being set in an HttpOnly cookie. `Controllers/AuthController.cs:59-63` exposes the token in register/login/refresh responses; the business-registration flow does the same at `Controllers/BusinessController.cs:431-438`. This defeats the intended protection of the HttpOnly cookie and makes token theft through frontend JavaScript or response logging possible. Return only the intended transport (prefer the cookie) and update the contract consistently.

3. Registration and login do not explicitly validate their authentication inputs beyond password confirmation and Identity password checks. Empty/whitespace phone numbers and names are not constrained by DTO validation, and invalid/empty request fields can reach Identity or produce inconsistent responses. Add explicit request validation and tests for required/format constraints.

4. Registration ignores failures from role creation and role assignment. `Controllers/AuthController.cs:88-91` does not inspect either result. A user can be created successfully and then receive an authentication response without the required `Customer` role if role setup fails. Handle the results transactionally and do not issue tokens when role assignment fails.

5. Business registration is authorized for any authenticated user and changes the user's Identity role to `BusinessOwner`, but the existing access token remains unchanged. `Controllers/BusinessController.cs:350-396` does not invalidate/reissue existing access tokens until the client uses the newly returned token. This can leave authorization state inconsistent after role elevation; define and enforce a token/role refresh or revocation strategy.

## Coverage gaps (not confirmed runtime bugs)

- Existing tests cover only successful registration/login, duplicate registration, incorrect password, missing refresh cookie, and logout revocation.
- No executable coverage exists for malformed/expired/incorrectly signed JWTs, role restrictions, unknown users, invalid payloads, password confirmation failures, refresh rotation/reuse, cookie attributes, expired/revoked refresh tokens, session rejection, or cross-user access.
- The test fixture depends on a running Docker daemon and PostgreSQL container; the current environment prevented all six tests from running.

## Files changed in this audit

- `context/progress-tracker.md` only.
- No production code or test code was changed.
