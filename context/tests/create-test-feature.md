# Task: Set Up Backend Test Infrastructure and Implement Auth Tests

The initial audit confirmed that the repository has no test project or test files. The backend builds successfully, but no authentication tests have been executed.

Now implement the missing test infrastructure and create meaningful automated tests for the existing authentication system.

## 1. Inspect Before Changing

- Inspect `BookingSaas.slnx`, `BookingSaas-backend.csproj`, target framework, project structure, dependency injection, and authentication implementation.
- Identify the actual Auth endpoints, DTOs, Identity configuration, JWT configuration, and refresh-token behavior.
- Check the existing EF Core provider and database configuration.
- Do not guess namespaces or production behavior.
- Do not modify production authentication logic during initial test setup.

## 2. Create a Test Project

Create a backend test project using xUnit and the compatible ASP.NET Core integration-testing packages for the repository's target framework.

- Follow the existing naming conventions.
- Add the test project to `BookingSaas.slnx`.
- Reference the backend project.
- Configure test discovery so `dotnet test` actually discovers and executes the tests.
- Use compatible package versions.
- Avoid unnecessary dependencies.

## 3. Isolate Test Infrastructure

- Never connect tests to the development or production PostgreSQL database.
- Determine the safest isolated database strategy based on the actual EF Core configuration.
- If the application relies on PostgreSQL-specific behavior, prefer an isolated test PostgreSQL instance or container if the existing environment supports it.
- Do not silently replace PostgreSQL with EF Core InMemory when testing relational behavior.
- Use a dedicated test environment and test-only JWT signing configuration.
- Replace or mock external services when necessary.
- Ensure tests clean up their data and can run repeatedly.

## 4. Implement the First Integration Tests

Start with these HTTP-level tests:

1. Registration succeeds with valid input.
2. Registering the same phone number twice is rejected.
3. Login succeeds with valid credentials.
4. Login with an incorrect password is rejected.
5. Refresh with a missing or invalid token is rejected.
6. Logout and refresh-token revocation behave according to the actual implementation.

Derive request payloads, routes, expected status codes, and response properties from the real code. Do not invent expected responses.

For each test, verify meaningful behavior rather than merely asserting that the endpoint returned a response.

## 5. Verify Test Discovery and Execution

Run:

`dotnet test BookingSaas.slnx --no-restore -v normal`

If newly added test dependencies have not been restored, run the appropriate restore command before testing.

Confirm that the test runner discovers and executes actual tests. Report the total discovered, passed, failed, and skipped tests.

Do not report success if zero tests were executed.

## 6. Final Report

Report:

- New files and project configuration changes.
- Test framework and database strategy.
- Test cases implemented.
- Exact commands executed.
- Test discovery count and results.
- Any environment blockers.
- Any confirmed production bugs discovered.
- The existing dependency vulnerability warning, including its package and severity if you can determine them.

Do not change production authentication behavior just to pass tests. If a test exposes a potential bug, report the evidence and recommend the smallest appropriate fix separately.

Implement this task now rather than stopping after the audit.