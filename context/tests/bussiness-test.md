# Task: Implement Backend Business Integration Tests

The backend authentication tests are implemented and passing. Continue by adding automated integration tests for the Business module in the BookingSaaS ASP.NET Core backend.

## Rules
- Inspect the actual implementation before writing tests.
- Reuse the existing test project, fixtures, authentication helpers, and database setup.
- Do not create a second test project.
- Do not modify production behavior merely to make tests pass.
- Do not guess endpoint routes, DTOs, role requirements, or response codes.
- Use isolated test data and never access production data.
- Keep changes limited to Business tests and any strictly necessary shared test infrastructure.

## Test Scenarios

### Business creation
- A valid BusinessOwner can create a business through the intended owner registration endpoint.
- An unauthenticated request is rejected if authentication is required.
- A Customer cannot create a business through an owner-only endpoint.
- Invalid required fields are rejected.
- Verify the created business is associated with the correct owner.
- Verify duplicate or conflicting data follows the actual business rules.

### Business retrieval
- The owner can retrieve their own business or businesses.
- An owner cannot retrieve another owner's private business data through owner-only endpoints.
- Public business listing or detail endpoints behave according to their intended access rules.
- Invalid or nonexistent business IDs are handled correctly.

### Business update
- The authorized owner can update permitted business fields.
- Another owner cannot modify the business.
- A Customer cannot update owner-only business information.
- Invalid input is rejected.
- Fields that must not be user-editable remain protected.

### Business deletion
- Inspect whether business deletion currently exists.
- If implemented, test authorization, ownership, and expected deletion behavior.
- If not implemented, report the missing feature; do not invent an endpoint or implement deletion as part of this task.

### Authorization and security
- Verify the actual role and ownership requirements from the controllers, policies, and services.
- Test Admin behavior according to the intended permissions.
- Ensure changing a business ID cannot bypass ownership checks.
- Verify failures do not expose internal exceptions or sensitive data.

## Execution
- Run the new Business tests and the complete existing backend test suite.
- Report passed, failed, and skipped counts.
- For failures, distinguish confirmed bugs from incorrect test assumptions and environment issues.
- Provide file paths and evidence for confirmed bugs.
- Do not silently fix production bugs. Report them separately and recommend minimal fixes.

Begin by inspecting the Business implementation, then add and execute the tests.