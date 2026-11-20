# Implementation Audit

**Audit date:** 2026-11-19  
**Scope:** tracked source, project files, configuration, migrations, and a
non-mutating build/test invocation. “Cannot verify” means execution would need
configured secrets, PostgreSQL, SMTP, or other local infrastructure not present
in the repository.

## Repository Inventory

| Area | Projects / implementation |
|---|---|
| Gateway | `src/ApiGateway/Fundoo.ApiGateway` (.NET 8, Ocelot) |
| User service | `Fundoo.User.Api`, `.Application`, `.Domain`, `.Infrastructure`; `UserDbContext`, `UserRepository`, EF Core migrations |
| Notes service | `Fundoo.Notes.Api`, `.Application`, `.Domain`, `.Infrastructure`; Npgsql connection factory plus `NotesRepository` and `LabelsRepository` |
| Notification service | `Fundoo.Notification.Api`, `.Application`, `.Domain`, `.Infrastructure`; `NotificationDbContext`, idempotency repository, MailKit SMTP sender |
| Controllers | `AuthController`, `AuthControllerV2`, `NotesController`, `LabelsController`, `NotificationController` |
| Configuration | Per-API `appsettings.json`, gateway `ocelot.json`, launch profiles, user secrets IDs |
| Absent artifacts | Dockerfiles/Compose, test projects, CI workflow, NLog config, FluentValidation, Kafka/event contracts, OAuth/OIDC configuration |

The root solution includes all gateway, API, application, domain, and
infrastructure projects. Notification also contains two nested solution files
that are not referenced by the root solution and should be rationalized in a
later, approved architecture pass.

## Implementation Matrix

| Requirement | Status | Evidence, gap, required modification, and test strategy |
|---|---|---|
| API routing and versioning | **IMPLEMENTED BUT INCOMPLETE** | Attribute routes exist in all controllers. User routes are versioned (`/api/v{version}/auth`) with GUID constraints in Notes; Notes and Notification are unversioned. `LabelsController` attaches labels at `/api/labels/{noteId}/labels/{labelId}`, an inconsistent resource route. Standardize route/version policy and verify with integration tests for each status code. |
| Controller/business separation | **IMPLEMENTED BUT INCOMPLETE** | User and Notes call services/repositories, but controllers shape ad-hoc responses and perform repeated claim parsing. Add shared error/identity handling and contract tests. |
| Registration and login | **IMPLEMENTED BUT INCOMPLETE** | `AuthService` persists users and verifies credentials; V1/V2 endpoints work by code inspection. Registration creates an unused access token and does not publish a registration event or welcome notification. Verify against PostgreSQL. |
| JWT validation and access tokens | **IMPLEMENTED BUT INCOMPLETE** | `JwtTokenService` signs HMAC-SHA256 tokens; User and Notes validate issuer, audience, key, and lifetime. Tokens contain only `sub` and `email`; no role, `jti`, or authorization policy. Signing key is expected from configuration, not committed. Add options validation, role/claims, key-strength validation, and authentication tests. |
| Refresh tokens and logout | **IMPLEMENTED INCORRECTLY** | V2 stores SHA-256 token hashes, rotates transactionally, and detects family replay (`AuthService`, `RefreshToken`, `UserRepository`). There is no logout/revoke endpoint, password reset does not revoke sessions, and refresh token is returned in JSON rather than a hardened cookie. Add revocation endpoint/session policy and tests for replay, expiry, logout, and reset. |
| Authorization and resource ownership | **IMPLEMENTED BUT INCOMPLETE** | Notes APIs use `[Authorize]`, extract `sub`, and every notes/labels query filters by `user_id`. Notification has no authentication; roles, policies, and claims authorization are absent. Add service-to-service auth and policy/role requirements; test cross-user access. |
| Password hashing | **IMPLEMENTED INCORRECTLY** | `BCryptPasswordHasher` is used. The requested standard is ASP.NET Core Identity `PasswordHasher` (PBKDF2) with version/rehash support; neither rehash nor password-change endpoint exists. Replace only after approved Phase 4 migration plan; test legacy migration and timing-safe verification. |
| Password reset | **IMPLEMENTED BUT INCOMPLETE** | Reset tokens are random, SHA-256 hashed, 15-minute, one-use, and consumed transactionally. Reset notification sends the raw token to the notification service. Add validation, rate limits, refresh-token invalidation, durable delivery/outbox, and integration tests. |
| Sessions | **NOT IMPLEMENTED** | No server-side session or cookie configuration. This is appropriate for the current bearer-token approach, but document a separate secure OIDC browser-cookie session only if external login is introduced. |
| OAuth 2.0/OIDC/SSO | **NOT IMPLEMENTED** | No identity provider, authorization-code/PKCE handler, callback, account-linking, or SSO configuration. Select an IdP and design federation before implementation. |
| Two-factor authentication | **NOT IMPLEMENTED** | No TOTP secret, recovery-code storage, enrollment, or challenge flow. Requires a schema addition and security design approval. |
| User database (EF Core) | **IMPLEMENTED BUT INCOMPLETE** | `UserDbContext` maps users, reset tokens, and refresh tokens with key indexes and migrations; repository operations are asynchronous and sensitive updates use transactions. Add explicit entity configurations, password/role/2FA metadata, a unique refresh-token-hash index, migration deployment strategy, and database integration tests. |
| Notification database (EF Core) | **IMPLEMENTED INCORRECTLY** | Only notification idempotency state is persisted (`NotificationIdempotency`); no notification history, recipient, body metadata, delivery result, attempt count, or retry schedule. Add a notification delivery aggregate and migration; verify delivery outcomes in integration tests. |
| Notes database (ADO.NET) | **IMPLEMENTED BUT INCOMPLETE** | Npgsql `Connection`, `Command`, parameters, readers, async calls, disposal, and an ownership transaction are used in repositories. This meets the intent of ADO.NET despite PostgreSQL types rather than SQL Server. Schema creation/migrations are absent, DI has a stale `Fundoo.Notes.Application.Repositories` using, and connection setup is only runtime-verifiable. Add versioned SQL migrations and repository integration tests. |
| FluentValidation | **NOT IMPLEMENTED** | Data annotations and manual checks exist, but no FluentValidation package, validators, or pipeline registration. Add validators and a consistent ProblemDetails response; test automatic execution. |
| Global exception handling | **NOT IMPLEMENTED** | Controllers handle selected errors; no `UseExceptionHandler`, `IExceptionHandler`, correlation ID middleware, or ProblemDetails configuration. Add centralized safe handling and API tests. |
| NLog and correlated logging | **NOT IMPLEMENTED** | Default ASP.NET logging and a few structured `ILogger` calls exist. No NLog provider/configuration, request logging, service identity, correlation propagation, file retention, or redaction policy. |
| SMTP | **IMPLEMENTED BUT INCOMPLETE** | `SmtpEmailService` uses MailKit, StartTLS, runtime settings, HTML templates, timeout, and disconnect cleanup. It has no plain-text alternative, retry policy, delivery persistence, configuration validation, or test SMTP integration. Credentials are not committed. |
| HttpClient and resilience | **IMPLEMENTED BUT INCOMPLETE** | User’s named Notification client uses `IHttpClientFactory` and `Microsoft.Extensions.Http.Resilience`; settings include base/attempt/total timeout and retry values. Audit the extension in Phase 2, then add safe-method/idempotency-aware policies, circuit breaker, and response tests. |
| Kafka and transactional outbox | **NOT IMPLEMENTED** | User registration does not call notification delivery; no Kafka package, producer, consumer, topic, consumer group, dead-letter handling, tracing, or outbox. Design an idempotent `UserRegistered` event and outbox before schema changes. |
| Rate limiting | **IMPLEMENTED BUT INCOMPLETE** | Ocelot route limits exist and depend on `X-Client-Id`, which callers can spoof; no ASP.NET rate limiter, authenticated-user key, endpoint-specific policies, or downstream limits. Add a trusted identity partition and tests. |
| Retry, timeout, circuit breaker | **IMPLEMENTED BUT INCOMPLETE** | Notification client settings and resilience package exist; no evidence of a validated circuit-breaker strategy or broader outbound policy. Add narrowly scoped idempotent-operation policies and fault tests. |
| Ocelot gateway | **IMPLEMENTED BUT INCOMPLETE** | Gateway loads `ocelot.json`, proxies User v1/v2 and Notes, and has `/health`. Notification is not routed; gateway has no authentication/exception middleware, downstream health-aware routing, or secure production endpoint configuration. |
| Load balancing | **NOT IMPLEMENTED** | Each gateway route has one localhost downstream instance and no load-balancer option. Add two containerized instances and health checks before claiming distribution. |
| Docker/local environment | **NOT IMPLEMENTED** | No Dockerfile, Compose file, Kafka/PostgreSQL/Mailpit services, environment template, or automated migration/start-up workflow exists. |
| Automated tests | **NOT IMPLEMENTED** | No test project is tracked. `dotnet test FundooMicroservice.slnx --no-restore --no-build` produced no test output. Add unit, integration, containerized infrastructure, and gateway tests by phase. |

## Build and Risk Findings

`dotnet build FundooMicroservice.slnx --no-restore` was invoked. It emitted
`NU1903` high-severity vulnerability warnings for
`Microsoft.AspNetCore.WebUtilities` 8.0.23 in the User API, Application,
Domain, and Infrastructure projects. The sandbox invocation did not emit a
final success/failure summary, so end-to-end build status is **CANNOT VERIFY**;
the observed projects compiled without reported errors before output ended.

Priority risks are: vulnerable package versions, missing secret/bootstrap
configuration, unauthenticated notification endpoints, no central error
boundary, no durable registration event/outbox, and no automated tests or local
infrastructure.

## Phase 2 Entry Criteria

Before feature work, correct compile/reference and package-version issues,
validate solution membership, establish configuration validation, and introduce
a repeatable baseline build/test command. Database migrations, identity schema,
and architectural replacements remain deferred until explicitly approved.

## Phase 2 Results (2026-11-19)

### Dependency and Project Consistency

All 13 project references resolve to existing projects. No project-reference
correction was needed: each API references its Application and Infrastructure
layers, and Infrastructure references the layers it implements. The root
`.slnx` builds correctly; `dotnet list <solution>.slnx reference` is not
supported by the installed CLI, so reference validation was performed against
every `ProjectReference` path instead.

Removed unused direct dependencies after source inspection found no usages:

- `Microsoft.AspNetCore.WebUtilities` from all User projects and Notification
  Application/Domain. This removes the vulnerable 8.0.23 User references and
  keeps Domain/Application layers free of unnecessary ASP.NET dependencies.
- `Npgsql` from Notes Application and `Npgsql.EntityFrameworkCore.PostgreSQL`
  from Notes Infrastructure. Notes continues to use its intended ADO.NET Npgsql
  repository implementation.
- Added `Microsoft.Extensions.Configuration.Abstractions` 8.0.0 explicitly to
  Notes Infrastructure because it uses `IConfiguration`; it was previously an
  accidental transitive dependency of the removed EF provider.

The removed WebUtilities version was affected by
[GHSA-x33c-w48q-qrgc / CVE-2026-114149](https://github.com/advisories/GHSA-x33c-w48q-qrgc),
a high-severity multipart-header-count denial-of-service issue. The patched
.NET 8 package version is 8.0.34. Removal is the least-risk remediation because
the application has no source usage of that package.

The scan then exposed separate vulnerable transitive packages inherited from
Notification's EF Core/Npgsql 8.0.0 set. Updated only that coherent dependency
family to 8.0.11:

- `Microsoft.EntityFrameworkCore`, `.Design`, and `.Tools`
- `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`
- `Npgsql.EntityFrameworkCore.PostgreSQL`

This resolves the reported `Microsoft.Extensions.Caching.Memory` 8.0.0,
`Npgsql` 8.0.0, and `System.Text.Json` 8.0.0 transitives (including
GHSA-qj66-m88j-hmgj, GHSA-x9vc-6hfv-hg8c, GHSA-hh2w-p6rv-4g7w, and
GHSA-8g4q-xg66-9fp4). All selected versions target .NET 8 and match the EF/Npgsql
minor line already used by User Infrastructure.

### Configuration and Startup Review

All committed `appsettings*.json`, `ocelot.json`, and launch-profile JSON files
parse successfully. User, Notes, and Notification intentionally omit database
connection strings and secret settings from committed files; their API projects
have `UserSecretsId` values. Required runtime values are:

- User: `ConnectionStrings__UserDb`, `Jwt__SigningKey`.
- Notes: `ConnectionStrings__NotesDb`, `Jwt__SigningKey`.
- Notification: `ConnectionStrings__NotificationDb` and `EmailSettings` host,
  port, sender, username, and password.

No placeholder credentials were added. Gateway configuration is syntactically
valid and uses HTTPS downstream URLs; its `BaseUrl` (`http://localhost:7000`)
does not match the HTTPS launch URL (`https://localhost:7000`) and remains a
documented deployment decision for the Docker/gateway phase rather than an
unverified behavior change.

### Verification

```powershell
dotnet restore FundooMicroservice.slnx --force-evaluate
dotnet build FundooMicroservice.slnx --no-restore
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object {
  dotnet list $_.FullName package --vulnerable --include-transitive
}
```

Final restore completed. Final solution build succeeded with **0 warnings and
0 errors**. Every project-level vulnerable-package scan reported no vulnerable
direct or transitive packages. `git diff --check` passed. No test projects
exist, so no automated tests were run in this phase.

Remaining technical issues are feature-level work deferred by scope: missing
runtime secrets/infrastructure for startup execution, no test suite or CI,
gateway HTTP/HTTPS BaseUrl decision, and the broader security, messaging,
resilience, and deployment gaps listed in the matrix above.

## Phase 3 Results (2026-11-19)

### User Service — EF Core

User persistence continues to use `UserDbContext` and `UserRepository` with
async EF Core operations and transactions for password-reset consumption and
refresh-token rotation. The model now explicitly declares the existing unique
`RefreshTokens.TokenHash` index, matching the database snapshot and preventing
an accidental future migration from weakening token-hash uniqueness. No User
schema migration was required or applied.

### Notification Service — EF Core

Added a delivery-history aggregate without storing sensitive notification
content. `NotificationDelivery` records type, recipient metadata, idempotency
key, status, attempt count, timestamps, and a bounded exception type only.
Reset tokens and rendered email bodies are deliberately not persisted.

`NotificationDeliveryRepository` persists pending entries and atomically marks
success or failure with EF Core `ExecuteUpdateAsync`. The notification service
now records outcomes for password-reset and welcome emails, while preserving
the existing idempotency flow. DI registers the new repository.

The additive migration `20261119150000_AddNotificationDeliveries` creates the
new table and indexes only; it does not alter or remove existing data. EF CLI
migration discovery confirmed both Notification migrations. The migration was
not applied because no database credentials or deployment environment are part
of this repository.

### Notes Service — ADO.NET

Added the versioned PostgreSQL script
`src/Services/Notes/Fundoo.Notes.Infrastructure/Database/Migrations/001_initial_schema.sql`.
It defines `notes`, `labels`, and `note_labels`, foreign keys for local note/
label relations, and query-aligned indexes. The script is intended for an empty
database via the deployment workflow; it is not executed automatically.

The existing repositories remain ADO.NET/Npgsql based, use asynchronous
commands and parameters, and keep ownership constraints in SQL. Null note
content is normalized to an empty string at the API/repository boundary so it
matches the non-null database column and domain model.

### Verification

`dotnet build FundooMicroservice.slnx --no-restore` succeeded with **0 warnings
and 0 errors**. `dotnet ef migrations list --no-connect` discovered both
Notification migrations using an ephemeral, non-connected validation string.
`git diff --check` passed. No database was modified and no database integration
tests could run because PostgreSQL connection strings and a test environment are
not available.

Remaining database work: apply migrations through a controlled deployment,
add integration tests against disposable PostgreSQL instances, add a migration
runner/deployment policy, and implement future identity/outbox schema additions
only in their approved phases.
