# Repository Guidelines

## Project Structure & Module Organization

This is a .NET 8 microservices solution. The root solution file,
`FundooMicroservice.slnx`, includes the API gateway at
`src/ApiGateway/Fundoo.ApiGateway` and three services under `src/Services/`:
`User`, `Notes`, and `Notification`.

Each service follows a layered layout:

- `*.Api` contains ASP.NET Core entry points, controllers, contracts,
  configuration, health checks, and `.http` request samples.
- `*.Application` contains service logic, DTOs, abstractions, and interfaces.
- `*.Domain` contains entities and domain enums.
- `*.Infrastructure` contains EF Core persistence, repositories, security, and
  dependency-injection registrations.

Keep dependencies directed inward: API depends on application/infrastructure;
infrastructure implements application abstractions; domain stays framework-free.

## Build, Test, and Development Commands

Run commands from the repository root:

```powershell
dotnet restore FundooMicroservice.slnx    # Restore NuGet packages
dotnet build FundooMicroservice.slnx      # Compile every project
dotnet test FundooMicroservice.slnx       # Run tests when test projects exist
dotnet run --project src/Services/Notes/Fundoo.Notes.Api
dotnet run --project src/ApiGateway/Fundoo.ApiGateway
```

Use the service-specific `.http` files (for example,
`src/Services/User/Fundoo.User.Api/Fundoo.User.Api.http`) for local endpoint
checks. Build the solution before submitting changes; there are currently no
committed test projects or coverage threshold.

## Coding Style & Naming Conventions

Use C# conventions already present in the codebase: four-space indentation,
file-scoped namespaces where applicable, nullable reference types enabled, and
implicit usings enabled. Use PascalCase for types, public members, files, and
methods; use camelCase for locals and parameters. Name interfaces with `I`
(for example, `INotesRepository`), request models with `Request`, and response
models with `Response`. Keep controllers thin and place business logic behind
application services and abstractions. No formatter or analyzer configuration is
committed; avoid unrelated formatting changes.

## Testing Guidelines

When adding tests, create a clearly named test project alongside the relevant
service (for example, `Fundoo.Notes.Application.Tests`). Name tests by behavior,
such as `CreateNote_WhenTitleIsMissing_ReturnsValidationError`. Cover new
application logic and important API error paths, then run `dotnet test` from the
root.

## Commit & Pull Request Guidelines

Existing history uses short, imperative summaries (for example, `Updated Folder
Structure` and `Project Reference Bug fixed`). Continue with focused commit
subjects that state the change. Pull requests should describe scope and
verification, link the related issue when available, and include request/response
examples or screenshots for API or Swagger-visible changes. Do not commit secrets;
use user secrets or environment-specific configuration for credentials.
