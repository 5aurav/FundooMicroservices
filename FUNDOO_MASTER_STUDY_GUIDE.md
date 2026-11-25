# FundooMicroservices: Complete Master Engineering & Interview Guide

> **System Overview & Technology Stack Snapshot**
>
> - **Core Framework**: .NET 8 (ASP.NET Core Web API, C# 12)
> - **API Gateway**: Ocelot Reverse Proxy with RoundRobin Load Balancing, Route Rate Limiting, and JWT Validation
> - **Microservices**: 
>   - `Fundoo.User`: Identity, PBKDF2/BCrypt, Keycloak OAuth2/OIDC, 2FA, Sessions, Outbox Pattern
>   - `Fundoo.Notes`: Notes & Labels CRUD, ADO.NET (Npgsql direct), Redis Distributed Cache
>   - `Fundoo.Notification`: Kafka Consumer & MailKit SMTP (Gmail / MailHog)
> - **Shared Foundation**: `Fundoo.Common` (RFC 7807 GlobalExceptionHandler, CorrelationIdMiddleware, InternalServiceAuthMiddleware, KafkaProducer, RedisDistributedCache)
> - **Persistence**: PostgreSQL 16 (Independent DBs: `fundoo_user`, `fundoo_notes`, `fundoo_notification`), EF Core 8 & raw Npgsql
> - **Distributed Messaging & Caching**: Apache Kafka 7.6 + Zookeeper, Redis 7 In-Memory Cache
> - **DevOps & Cloud**: Multi-Stage Dockerfiles, Docker Compose, Kubernetes Deployments & NodePort (Port 30080), Jenkins CI/CD

---

## Table of Contents
1. [PART A — THEORY & ENGINEERING CONCEPTS](#part-a--theory--engineering-concepts)
   - [Module 1: Project Architecture and Design](#module-1-project-architecture-and-design)
   - [Module 2: Backend Development & REST APIs](#module-2-backend-development--rest-apis)
   - [Module 3: Authentication and Authorization](#module-3-authentication-and-authorization)
   - [Module 4: Database Management & Performance](#module-4-database-management--performance)
   - [Module 5: Microservices Architecture & Patterns](#module-5-microservices-architecture--patterns)
   - [Modules 6–8: Docker, Kubernetes & CI/CD](#modules-68-docker-kubernetes--cicd)
   - [Modules 9–14: Linux, Networking, Cloud, Security, Testing & Monitoring](#modules-914-linux-networking-cloud-security-testing--monitoring)
2. [PART B — PRACTICAL RUNBOOK & COMMANDS](#part-b--practical-runbook--commands)
3. [PART C — ARCHITECTURE FLOWS & DEEP-DIVE QUESTIONS](#part-c--architecture-flows--deep-dive-questions)
   - [Flow 1: User Registration & Asynchronous Notification Flow](#flow-1-user-registration--asynchronous-notification-flow)
   - [Flow 2: Login & JWT + Refresh Token Rotation Flow (Replay Detection)](#flow-2-login--jwt--refresh-token-rotation-flow-replay-detection)
   - [Flow 3: Note Creation & Redis Distributed Caching Flow](#flow-3-note-creation--redis-distributed-caching-flow)
   - [Flow 4: Password Reset & Resilient Notification Flow](#flow-4-password-reset--resilient-notification-flow)
   - [Flow 5: Zero-Trust Inter-Service Security Flow](#flow-5-zero-trust-inter-service-security-flow)
   - [Flow 6: Database Migration Execution Startup Flow](#flow-6-database-migration-execution-startup-flow)
   - [Flow 7: CI/CD & Kubernetes Rolling Update Flow](#flow-7-cicd--kubernetes-rolling-update-flow)
4. [PART D — PROJECT OWNERSHIP INTERVIEW GUIDE](#part-d--project-ownership-interview-guide)
   - [23 Core Ownership Questions & Answers](#23-core-ownership-questions--answers)
   - [Top 10 Live Practical Demonstrations](#top-10-live-practical-demonstrations)

---

# PART A — THEORY & ENGINEERING CONCEPTS

## Module 1: Project Architecture and Design

### 1.1 Problem Statement & Purpose
In traditional monolithic architectures, user authentication, rich-text note operations, and email notifications share CPU, memory, thread pools, and database connections. A burst of password reset emails or a heavy search query on notes can saturate thread pools or database locks, taking down authentication and rendering the entire application unusable.

**FundooMicroservices** decomposes the note-taking domain (inspired by Google Keep) into independently scalable, resilient, and loosely coupled microservices (`User`, `Notes`, `Notification`) that each own their respective databases and communicate through an API Gateway and an asynchronous event bus.

### 1.2 Functional & Non-Functional Requirements
| Category | Requirement | Fundoo Implementation |
|---|---|---|
| **Functional** | User Authentication | Registration, Login (PBKDF2/BCrypt), JWT access & refresh token rotation, TOTP 2FA, session tracking, and Keycloak SSO. |
| **Functional** | Notes & Labels CRUD | Create, Read, Update, Delete, Archive, Trash (soft delete), Pin/Unpin, Color coding, and M:N label mapping. |
| **Functional** | Automated Notifications | Asynchronous welcome emails on registration and secure 15-minute password reset links via MailKit SMTP. |
| **Non-Functional** | Low Latency & High Throughput | Kestrel server (.NET 8), Redis distributed caching for hot notes, and ADO.NET (raw Npgsql) for SQL execution. |
| **Non-Functional** | Fault Isolation | Database-per-service isolation; Notification outage does not block note creation or user registration (buffered by Kafka). |
| **Non-Functional** | Zero-Trust Security | Gateway injects `X-Internal-Service-Key`; downstream services reject direct external calls via `InternalServiceAuthMiddleware`. |
| **Non-Functional** | Observability | NLog structured logging with `X-Correlation-Id` propagated across services and surfaced in RFC 7807 `ProblemDetails`. |

### 1.3 Clean Layered Architecture
Each service is split into four inward-directed layers:
```
┌─────────────────────────────────────────────────────────────┐
│                    *.Api (Presentation)                     │
│  Controllers, Contracts (Requests/Responses), Program.cs    │
└──────────────────────────────┬──────────────────────────────┘
                               │ references
┌──────────────────────────────▼──────────────────────────────┐
│                  *.Application (Use Cases)                  │
│     Interfaces, DTOs, Service Contracts, Business Rules     │
└───────────────▲──────────────────────────────▲──────────────┘
                │ implements                   │ references
┌───────────────┴──────────────┐┌──────────────┴──────────────┐
│ *.Infrastructure (Data/Ext)  ││       *.Domain (Entities)   │
│ DbContext, Repositories,     ││ Pure Domain Entities, Enums │
│ Npgsql, Kafka, SMTP, Redis   ││ (Framework & DB Free!)      │
└──────────────────────────────┘└─────────────────────────────┘
```

---

## Module 2: Backend Development & REST APIs

### 2.1 HTTP Request & Response Envelope Example
```http
POST /api/notes HTTP/1.1
Host: localhost:7000
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
X-Correlation-Id: 5c9e2b10-7e18-4e8a-a43b-2879ef4901b2
Content-Type: application/json

{
  "title": "System Design Architecture",
  "content": "Review Microservices, Kafka Outbox, and Circuit Breakers.",
  "color": "#FEF08A",
  "isPinned": true
}

HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8
Location: /api/notes/a31f24d1-8b36-4c47-8a65-1d4e689bf150
X-Correlation-Id: 5c9e2b10-7e18-4e8a-a43b-2879ef4901b2

{
  "isSuccess": true,
  "message": "Note created successfully.",
  "data": {
    "id": "a31f24d1-8b36-4c47-8a65-1d4e689bf150",
    "title": "System Design Architecture",
    "content": "Review Microservices, Kafka Outbox, and Circuit Breakers.",
    "color": "#FEF08A",
    "isPinned": true,
    "isArchived": false,
    "isTrash": false,
    "createdAtUtc": "2026-11-24T23:20:00Z"
  }
}
```

### 2.2 PUT vs. PATCH
- **PUT**: Replaces the entire resource representation (idempotent). If fields are omitted, they are reset to defaults.
  - *Example*: `PUT /api/notes/{id}` replaces title, content, color, and isPinned.
- **PATCH**: Modifies a specific delta of the resource.
  - *Example*: `PATCH /api/notes/{id}/pin` toggles or updates only the pin status without touching title or content.

### 2.3 Global Exception Handling (RFC 7807 ProblemDetails)
Implemented via ASP.NET Core 8's `IExceptionHandler` in `Fundoo.Common.Middleware.GlobalExceptionHandler`:
```csharp
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var statusCode = exception switch {
            ValidationException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext);
        _logger.LogError(exception, "Unhandled exception | Path:{Path} Status:{Status} CorrelationId:{CorrelationId}",
            httpContext.Request.Path, statusCode, correlationId ?? "-");

        var problemDetails = new ProblemDetails {
            Status = statusCode,
            Title = exception switch {
                ValidationException => "Validation failed",
                NotFoundException => "Resource not found",
                _ => "Internal server error"
            },
            Detail = exception is NotFoundException or ValidationException ? exception.Message : "An unexpected error occurred.",
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        if (correlationId is not null) problemDetails.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);
        return true;
    }
}
```

---

## Module 3: Authentication and Authorization

### 3.1 Hybrid Password Hasher (PBKDF2 vs. BCrypt)
Fundoo implements automatic hash upgrade in `PasswordHasher.cs`:
```csharp
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _pbkdf2Hasher = new();

    public string Hash(string password) => _pbkdf2Hasher.HashPassword(null!, password);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash)) return false;

        // BCrypt hashes start with $2a$, $2b$, or $2y$
        if (IsBCryptHash(hash)) return BCrypt.Net.BCrypt.Verify(password, hash);

        // PBKDF2 hash (ASP.NET Core Identity standard)
        var result = _pbkdf2Hasher.VerifyHashedPassword(null!, hash, password);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }

    public bool NeedsRehash(string hash) => IsBCryptHash(hash);

    private static bool IsBCryptHash(string hash) =>
        hash.StartsWith("$2a$", StringComparison.Ordinal) ||
        hash.StartsWith("$2b$", StringComparison.Ordinal) ||
        hash.StartsWith("$2y$", StringComparison.Ordinal);
}
```

### 3.2 JWT Generation & Claims
```csharp
public string CreateAccessToken(AppUser user)
{
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SigningKey"]!));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: _configuration["Jwt:Issuer"],
        audience: _configuration["Jwt:Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "30")),
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

---

## Module 4: Database Management & Performance

### 4.1 Database per Service Pattern
- `fundoo_user`: EF Core 8 migrations (`Users`, `RefreshTokens`, `PasswordResetTokens`, `Sessions`, `Outbox`).
- `fundoo_notes`: Raw ADO.NET (Npgsql) with `001_initial_schema.sql` (`notes`, `labels`, `note_labels`).
- `fundoo_notification`: EF Core 8 (`NotificationIdempotency`, `NotificationDeliveries`).

### 4.2 High-Throughput ADO.NET Query
```csharp
public async Task<Note?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
{
    const string sql = @"
        SELECT id, user_id, title, content, color, is_pinned, is_archived, is_trashed, created_at, updated_at
        FROM notes
        WHERE id = @id AND user_id = @userId;";

    await using var conn = await _connectionFactory.CreateConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("id", id);
    cmd.Parameters.AddWithValue("userId", userId);

    await using var reader = await cmd.ExecuteReaderAsync(ct);
    if (!await reader.ReadAsync(ct)) return null;

    return MapRowToNote(reader);
}
```

---

## Module 5: Microservices Architecture & Patterns

### 5.1 Ocelot Gateway Route Definition
```json
{
  "UpstreamPathTemplate": "/api/notes/{everything}",
  "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ],
  "DownstreamPathTemplate": "/api/notes/{everything}",
  "DownstreamScheme": "http",
  "DownstreamHostAndPorts": [
    { "Host": "notes-api-1", "Port": 8080 },
    { "Host": "notes-api-2", "Port": 8080 }
  ],
  "LoadBalancerOptions": {
    "Type": "RoundRobin"
  },
  "AuthenticationOptions": {
    "AuthenticationProviderKey": "FundooGatewayKey"
  },
  "AddHeadersToRequest": {
    "X-Internal-Service-Key": "FundooInternalMicroservicesSecretToken_2026_Secure!"
  },
  "RateLimitOptions": {
    "EnableRateLimiting": true,
    "Period": "1m",
    "Limit": 50
  }
}
```

---

## Modules 6–8: Docker, Kubernetes & CI/CD

### 6.1 Multi-Stage Dockerfile (Notes API)
```dockerfile
# Stage 1: Build & Publish (SDK Image)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/Shared/Fundoo.Common/Fundoo.Common.csproj src/Shared/Fundoo.Common/
COPY src/Services/Notes/Fundoo.Notes.Api/Fundoo.Notes.Api.csproj src/Services/Notes/Fundoo.Notes.Api/
RUN dotnet restore src/Services/Notes/Fundoo.Notes.Api/Fundoo.Notes.Api.csproj

COPY src/Shared/Fundoo.Common/ src/Shared/Fundoo.Common/
COPY src/Services/Notes/ src/Services/Notes/
WORKDIR /app/src/Services/Notes/Fundoo.Notes.Api
RUN dotnet publish -c Release -o /out

# Stage 2: Final Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Fundoo.Notes.Api.dll"]
```

### 7.1 Kubernetes Manifests
```yaml
apiVersion: v1
kind: Service
metadata:
  name: gateway-public-service
spec:
  type: NodePort
  selector:
    app: api-gateway
  ports:
  - port: 80
    targetPort: 8080
    nodePort: 30080
```

---

# PART B — PRACTICAL RUNBOOK & COMMANDS

```bash
# 1. Local CLI Compilation and Test Execution
dotnet restore FundooMicroservice.slnx
dotnet build FundooMicroservice.slnx --no-restore
dotnet test FundooMicroservice.slnx --no-build --verbosity normal

# 2. Docker Compose Deployment & Verification
docker compose up -d --build
docker compose ps
docker logs -f fundoo-user-api-1
docker exec -it fundoo-notes-db psql -U postgres -d fundoo_notes -c "\dt;"

# 3. Kubernetes Deployment & Rollout Operations
kubectl apply -f k8s/databases.yaml
kubectl apply -f k8s/fundoo-k8s.yaml
kubectl get pods -A
kubectl get svc,deployments,endpoints

# 4. Troubleshooting a Failing / CrashLoopBackOff Pod
kubectl describe pod <pod-name>
kubectl logs <pod-name> --previous
kubectl exec -it <pod-name> -- sh

# 5. Zero-Downtime Rollback
kubectl rollout history deployment/user-deploy
kubectl rollout undo deployment/user-deploy
kubectl rollout status deployment/user-deploy
```

---

# PART C — ARCHITECTURE FLOWS & DEEP-DIVE QUESTIONS

### Flow 1: User Registration & Asynchronous Notification Flow
```
[1] CLIENT POST REQUEST
    ➜ Sends POST /api/v2/auth/register with user credentials to Gateway (Port 7000).
       │
       ▼
[2] GATEWAY ROUTING & RATE LIMITING
    ➜ Ocelot checks rate limit (20 req/min), adds X-Internal-Service-Key, routes RoundRobin to User API.
       │
       ▼
[3] VALIDATION & PASSWORD HASHING
    ➜ FluentValidation verifies payload; PasswordHasher computes PBKDF2 hash (100,000 iterations).
       │
       ▼
[4] DATABASE PERSISTENCE (USERDB)
    ➜ UserRepository inserts AppUser record into fundoo_user PostgreSQL database.
       │
       ▼
[5] OUTBOX / KAFKA EVENT PUBLISHING
    ➜ Publishes UserRegisteredEvent { UserId, Email, FirstName } to Kafka topic 'user-registered'.
       │
       ▼
[6] HTTP 201 RESPONSE RETURNED
    ➜ Returns 201 Created immediately to client. Client does NOT wait for email sending!
       │
       ▼
[7] KAFKA CONSUMER PROCESSING
    ➜ KafkaNotificationConsumerService in Notification API consumes event with idempotency check.
       │
       ▼
[8] EMAIL DISPATCH VIA MAILKIT
    ➜ Sends HTML welcome email via MailKit SMTP (Gmail / MailHog on port 587).
       │
       ▼
[9] DELIVERY LOG PERSISTED
    ➜ Writes audit record to NotificationDeliveries in fundoo_notification database.
```

### Flow 2: Login & Refresh Token Rotation with Replay Detection
```
[1] CLIENT LOGIN REQUEST
    ➜ POST /api/v2/auth/login with Email and Password.
       │
       ▼
[2] CREDENTIAL VERIFICATION
    ➜ AuthService verifies PBKDF2 (or migrates legacy BCrypt hash).
       │
       ▼
[3] TOKEN PAIR GENERATION
    ➜ Generates 30-min HMAC-SHA256 JWT and random 64-byte Refresh Token.
       │
       ▼
[4] DATABASE STORAGE
    ➜ Stores SHA-256 hash of refresh token in PostgreSQL (RefreshTokens table).
       │
       ▼
[5] TOKEN REFRESH INVOCATION
    ➜ Client sends POST /api/v2/auth/refresh with Refresh Token.
       │
       ▼
[6] REPLAY DETECTION CHECK
    ➜ Is token already revoked/used?
       ├── IF YES: SECURITY ALERT! Revoke entire token family and return 401 Unauthorized.
       └── IF NO: Mark old token revoked, transactionally issue new JWT & Refresh Token.
```

### Flow 3: Note Creation & Redis Distributed Caching Flow
```
[1] CLIENT POST /API/NOTES
    ➜ Client attaches Authorization: Bearer <jwt> and note JSON payload.
       │
       ▼
[2] GATEWAY VALIDATION
    ➜ Ocelot validates JWT signature, audience, and lifetime.
       │
       ▼
[3] SUB CLAIM EXTRACTION
    ➜ NotesController extracts user UUID from User sub claim.
       │
       ▼
[4] ADO.NET PARAMETERIZED INSERT
    ➜ NotesRepository executes INSERT INTO notes (user_id, title, content) VALUES (@userId, @title, @content) RETURNING id.
       │
       ▼
[5] CACHE INVALIDATION
    ➜ Deletes Redis key 'notes:user:{userId}:active' so subsequent reads fetch fresh data.
       │
       ▼
[6] HTTP 201 CREATED RETURNED
    ➜ Returns created note with GUID and timestamps.
```

### Flow 4: Password Reset & Resilient Notification Flow
```
[1] FORGOT PASSWORD REQUEST
    ➜ Client submits POST /api/v2/auth/forgot-password with email.
       │
       ▼
[2] ONE-TIME TOKEN GENERATION
    ➜ Generates 32-byte crypto token, stores SHA-256 hash in DB (15-min expiration).
       │
       ▼
[3] RESILIENT INTER-SERVICE CALL
    ➜ User calls Notification API via IHttpClientFactory with resilience policy (3s timeout, 3 retries).
       │
       ▼
[4] IDEMPOTENT EMAIL SEND
    ➜ Notification checks NotificationIdempotency table, dispatches email with reset token.
       │
       ▼
[5] TRANSACTIONAL CONSUMPTION
    ➜ User submits new password; DB transaction consumes token, updates hash, revokes sessions.
```

### Flow 5: Zero-Trust Inter-Service Security Flow
```
[1] EXTERNAL CLIENT CALL
    ➜ External client sends request to microservice or Gateway.
       │
       ▼
[2] GATEWAY SECRET INJECTION
    ➜ Ocelot automatically injects X-Internal-Service-Key: FundooInternalMicroservicesSecretToken_2026_Secure!
       │
       ▼
[3] SERVICE MIDDLEWARE INTERCEPTION
    ➜ Downstream service executes InternalServiceAuthMiddleware.
       │
       ▼
[4] SECRET EVALUATION
    ➜ If request arrives from outside and lacks valid secret header, immediately reject with HTTP 403 Forbidden!
```

---

# PART D — PROJECT OWNERSHIP INTERVIEW GUIDE

### 23 Core Ownership Questions & Answers

1. **What problem does your project solve?**
   FundooMicroservices provides a scalable, resilient backend for a cloud note-taking platform. It decomposes authentication, note management, and notifications into independent services, eliminating the coupling, scalability, and availability bottlenecks of traditional monoliths.

2. **Explain your complete project architecture.**
   Clients send requests to the Ocelot API Gateway (port 7000 / K8s 30080), which routes to `User`, `Notes`, and `Notification` microservices. Each service uses 4-layer Clean Architecture (`.Api`, `.Application`, `.Domain`, `.Infrastructure`) with shared `Fundoo.Common`, isolated PostgreSQL databases, Redis caching, Kafka messaging, Keycloak OIDC, and MailHog/SMTP.

3. **Why did you choose this technology stack?**
   .NET 8 provides top-tier asynchronous performance and memory efficiency. PostgreSQL provides ACID guarantees. EF Core was chosen for User/Notification for rapid migration management, while raw ADO.NET (Npgsql) was chosen in Notes for maximum SQL query speed. Kafka was chosen for reliable event streaming, and Redis for distributed sub-millisecond caching.

4. **Explain the complete request lifecycle.**
   Client -> Gateway (Ocelot validates JWT, checks rate limit, injects secret header, RoundRobin load balances) -> Downstream Service (`CorrelationIdMiddleware` -> `GlobalExceptionHandler` -> `InternalServiceAuth` -> `Authentication` -> `FluentValidationActionFilter`) -> Controller -> Application Service -> Repository -> PostgreSQL/Redis -> JSON Response.

5. **Explain the database schema.**
   `fundoo_user` contains Users, RefreshTokens, PasswordResetTokens, and Sessions. `fundoo_notes` contains notes, labels, and note_labels join table with compound indexes on `(user_id, is_trashed, is_archived)`. `fundoo_notification` contains NotificationIdempotency and NotificationDeliveries.

6. **Explain the authentication flow.**
   User registers/logs in via `/api/v2/auth/login`. PasswordHasher verifies PBKDF2 hash (or migrates legacy BCrypt). JwtTokenService issues a 30-minute HMAC-SHA256 JWT containing sub and email claims, and generates a random 64-byte refresh token whose SHA-256 hash is stored in PostgreSQL.

7. **Explain how authorization works.**
   Notes endpoints are decorated with `[Authorize]`. When a request arrives, the JWT middleware verifies signature, issuer, audience, and lifetime. The controller extracts the sub claim (user UUID) and passes it to NotesRepository, which appends `WHERE user_id = @userId` to every SQL query, preventing cross-tenant data access.

8. **Explain how services communicate.**
   Synchronously via `IHttpClientFactory` with `Microsoft.Extensions.Http.Resilience` (User -> Notification for password reset email requests) and asynchronously via Apache Kafka (User publishes `UserRegisteredEvent`; Notification consumes it to send welcome emails).

9. **Explain how you containerized the application.**
   Each service has a multi-stage Dockerfile using `mcr.microsoft.com/dotnet/sdk:8.0` for building and `mcr.microsoft.com/dotnet/aspnet:8.0` for lightweight runtime (~200MB). `docker-compose.yml` orchestrates the entire mesh with healthchecks and restart policies.

10. **Explain your Dockerfile.**
    Stage 1 copies `.csproj` files first and runs `dotnet restore` (taking advantage of Docker layer caching), then copies source and runs `dotnet publish -c Release`. Stage 2 copies the published output into a slim ASP.NET Core 8 runtime image, exposes port 8080, and configures the ENTRYPOINT.

11. **Explain your Kubernetes YAML files.**
    `databases.yaml` deploys PostgreSQL and Redis with ClusterIP services. `fundoo-k8s.yaml` deploys User, Notes, Notification (ClusterIP on port 80 targeting 8080) and API Gateway with a NodePort service exposing port 30080 externally.

12. **Explain your Jenkins pipeline.**
    A declarative Jenkinsfile triggered by GitHub webhooks runs 5 stages: Checkout, Restore & Build, xUnit Automated Tests, Docker Build & Push (tagged with `BUILD_NUMBER`), and Kubernetes Deployment (`kubectl set image` + `rollout status`).

13. **Explain how code reaches production.**
    Developer pushes to main -> GitHub Webhook triggers Jenkins -> Jenkins runs tests and builds Docker image -> pushes to Docker Hub -> updates Kubernetes deployment -> Kubernetes executes zero-downtime rolling update.

14. **Explain how external traffic reaches your application.**
    Client requests hit NodePort 30080 (or Ingress) -> routed to `gateway-public-service` -> Ocelot Gateway pod -> Ocelot proxies internally via CoreDNS to `user-service:80` or `notes-service:80` -> target pod port 8080.

15. **Explain where configuration and secrets are stored.**
    Application defaults reside in `appsettings.json` and `ocelot.json`. Local dev secrets use .NET User Secrets. Docker and Kubernetes inject environment variables (`ConnectionStrings`, `Jwt:SigningKey`). In production, credentials should be mounted from Kubernetes Secrets or HashiCorp Vault.

16. **Explain how you monitor the application.**
    NLog records structured logs enriched with `X-Correlation-Id`. Health endpoints (`/health`, `/health/live`, `/health/ready`) report database and Kafka readiness for Kubernetes probes. Docker and Kubernetes metrics track CPU/RAM usage.

17. **Explain how you troubleshoot application failures.**
    1. Check pod status with `kubectl get pods`. 2. Inspect events with `kubectl describe pod <name>`. 3. Check logs with `kubectl logs <name> --previous`. 4. Filter logs using Correlation ID to trace the exact failed request.

18. **What deployment problems did you encounter?**
    1. NuGet vulnerability NU1903 in EF Core/WebUtilities (resolved by upgrading to 8.0.11). 2. Gateway HTTP vs HTTPS downstream mismatch between local and container environments. 3. DB startup race conditions resolved by introducing Fundoo.MigrationRunner.

19. **What was the most difficult technical challenge?**
    Migrating legacy BCrypt password hashes to ASP.NET Core Identity PBKDF2 without forcing users to reset passwords. Solved by implementing a hybrid PasswordHasher that checks hash prefixes ($2a$) and performs lazy rehashing upon successful login.

20. **What would happen if a service or database went down?**
    If Notification goes down, User registration continues unaffected because Kafka buffers events. If Notes DB goes down, `/health/ready` fails and Kubernetes stops routing traffic to that pod while circuit breakers prevent thread pool starvation.

21. **How would you scale the application?**
    1. Horizontally scale Notes and User pods via Kubernetes Horizontal Pod Autoscaler (HPA) based on CPU/memory. 2. Add Redis caching to offload database reads. 3. Partition Kafka topics to allow parallel consumer instances. 4. Introduce PostgreSQL read replicas.

22. **What are the current limitations?**
    Plaintext secrets in Kubernetes YAML (need transition to K8s Secrets or Vault), single-instance Kafka broker (needs 3-broker cluster for production HA), and lack of OpenTelemetry distributed tracing.

23. **What would you improve in the next version?**
    Implement OpenTelemetry with Jaeger/Zipkin for distributed tracing, add Prometheus & Grafana dashboards for real-time alerting, convert PostgreSQL into a Kubernetes StatefulSet with persistent storage volume claims, and implement full Testcontainers integration tests.

### Top 10 Live Practical Demonstrations

1. **Run Complete Mesh Locally**: Execute `docker compose up -d --build` -> verify `docker compose ps` shows all containers healthy -> test `curl http://localhost:7000/health`.
2. **Trace an API Request**: Send `POST /api/notes` via Postman -> demonstrate Ocelot routing -> show Npgsql parameterized SQL execution in Notes logs.
3. **Demonstrate Authentication & Authorization**: Register new user -> login to receive JWT -> access `/api/notes` with Bearer token (200 OK) -> demonstrate 401 Unauthorized without token.
4. **Inspect Database Records**: Open PostgreSQL shell `docker exec -it fundoo-user-db psql -U postgres -d fundoo_user` -> query Users table to prove `password_hash` is PBKDF2/BCrypt (never plaintext).
5. **Build & Run Docker Containers**: Execute `docker build -t fundoo-notes:test -f src/Services/Notes/Fundoo.Notes.Api/Dockerfile .` -> run container with `docker run -p 8080:8080`.
6. **Deploy Application to Kubernetes**: Apply manifests with `kubectl apply -f k8s/` -> verify with `kubectl get pods,svc`.
7. **Debug a Failing Pod**: Simulate misconfiguration (e.g., bad DB connection string) -> observe CrashLoopBackOff -> diagnose via `kubectl describe pod` and `kubectl logs`.
8. **Trigger Jenkins Pipeline**: Push commit to Git repository -> monitor Jenkins build stages -> verify automated Docker push and Kubernetes rollout.
9. **External Traffic Verification**: Access application externally via NodePort: `curl http://<Node-IP>:30080/health` and verify HTTP 200 OK.
10. **Rollback a Failed Deployment**: Deploy a faulty image -> run `kubectl rollout undo deployment/user-deploy` -> verify zero-downtime recovery with `kubectl rollout status`.
