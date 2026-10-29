# Task: Audit and Implement Backend Authentication Tests

You are working on the BookingSaaS backend built with ASP.NET Core, ASP.NET Core Identity, JWT, Entity Framework Core, and PostgreSQL.

Your task is to inspect the existing authentication implementation, implement automated tests, run them, and report confirmed bugs.

## Important Rules

- Inspect the actual codebase before making changes.
- Discover the existing test framework, project structure, dependency injection, DTOs, controllers, and authentication configuration.
- Do not guess endpoint contracts or expected behavior.
- Do not refactor production authentication code just to make tests pass.
- Do not modify production code unless a confirmed bug requires a fix and the change is necessary for the requested task.
- Separate confirmed bugs from missing coverage and potential security concerns.
- Do not use a production database, real credentials, or real secrets.
- Keep the changes focused exclusively on backend authentication.

## Step 1: Inspect the Implementation

Inspect:

- AuthController and all actual authentication endpoints.
- Registration and login services.
- ASP.NET Core Identity configuration.
- JWT generation and validation.
- Refresh-token creation, hashing, persistence, rotation, expiration, and revocation.
- Cookie configuration and logout behavior.
- Role and claim configuration.
- Authentication middleware and authorization policies.
- Dependency injection registrations.
- Existing test projects and test infrastructure.

The reported endpoints are:

- `POST /api/Auth/register`
- `POST /api/Auth/login`
- `POST /api/Auth/refresh`
- `POST /api/Auth/logout`
- `GET /api/Auth/session`

Verify their actual routes and behavior before writing tests.

## Step 2: Implement Backend Tests

Use the existing testing framework. If no suitable test infrastructure exists, evaluate xUnit, `Microsoft.AspNetCore.Mvc.Testing`, and isolated test database options before introducing dependencies.

### Registration tests

- Successful registration.
- Duplicate phone number.
- Missing or invalid required fields.
- Invalid password according to configured Identity rules.
- Password confirmation mismatch, if supported.
- Correct default role assignment.
- Correct BusinessOwner role assignment for business registration, if applicable.
- Passwords are not exposed in responses or stored in plaintext.
- Correct HTTP status codes and response structure.

### Login tests

- Successful login with valid credentials.
- Incorrect password.
- Unknown phone number.
- Invalid request payload.
- Correct JWT claims and expiration.
- Failed login does not issue valid authentication tokens.
- Refresh-token issuance matches the actual API contract.

### Access-token tests

- Valid token grants access to protected endpoints.
- Missing, malformed, expired, or incorrectly signed tokens are rejected.
- Required roles and authorization policies are enforced.
- Subject and role claims are interpreted correctly.

### Refresh-token tests

- Valid refresh token succeeds.
- Invalid or expired refresh token is rejected.
- Revoked token is rejected.
- Rotated tokens cannot be reused when rotation is implemented.
- Newly issued tokens work as expected.
- Stored tokens are hashed if that is the implemented design.
- Missing refresh-token input is handled correctly.
- Body-based and cookie-based token transport are tested according to the actual implementation.
- Cookie expiration, HttpOnly, Secure, and SameSite settings match the intended environment.

### Logout tests

- Logout with a valid refresh token.
- Logout with a missing, invalid, or already-revoked token.
- Token revocation works correctly.
- Refresh cookie is cleared if cookie-based refresh is supported.
- A revoked token cannot be reused.

### Session and authorization tests

- Test the actual behavior of `GET /api/Auth/session`.
- Verify unauthenticated access is rejected where required.
- Test role restrictions for Customer, BusinessOwner, Employee, and Admin where applicable.
- Verify users cannot access another user's protected data by manipulating request parameters.
- Verify authentication errors do not expose stack traces, secrets, passwords, or sensitive tokens.

## Step 3: Test Quality

- Prefer integration tests for the complete HTTP authentication flow.
- Use unit tests for isolated token or authentication logic where valuable.
- Use an isolated test database and deterministic test configuration.
- Use test-only JWT signing keys.
- Do not require Cloudinary or external services.
- Avoid brittle tests that assert private implementation details unnecessarily.
- Do not weaken validation or authorization to make tests pass.

## Step 4: Run Tests and Report Findings

Run the backend test suite and report:

1. Files created or modified.
2. Scenarios covered.
3. Exact test commands.
4. Passed, failed, and skipped test counts.
5. Confirmed bugs with file paths and relevant code references.
6. Missing coverage and potential security risks.
7. Any production-code changes and why they were necessary.

If a test fails, identify whether the cause is a production bug, an incorrect test expectation, missing infrastructure, or an environment issue.

Start with inspection, then implement tests in focused changes. Do not perform an unrelated refactor.
do not change code. just report the bugs that need to be fixed 