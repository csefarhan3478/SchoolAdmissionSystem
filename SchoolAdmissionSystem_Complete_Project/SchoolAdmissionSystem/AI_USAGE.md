# AI usage disclosure

AI assistance was used to draft the initial project structure, API implementation, React UI, documentation and validation checklist.

## Areas assisted
- .NET Minimal API and EF Core entity/context scaffolding.
- PostgreSQL transaction and row-lock approach for seat allocation.
- React + TypeScript parent/admin interface.
- Docker Compose, Dockerfiles and setup documentation.

## Human review and verification required
Before submission, the developer must:
1. Run `docker compose up --build` and verify all three containers start.
2. Exercise all API routes through Swagger and the browser UI.
3. Test concurrent reservations against a class with limited seats.
4. Test expiry, confirmation idempotency, cancellation and persistence after restart.
5. Review the implementation and be able to explain each design decision.
6. Correct any build/runtime issues found in the target environment.

AI-generated code can contain defects. Do not claim a test passed unless it was actually run and observed. This disclosure should be updated to accurately describe the tools and assistance actually used.
