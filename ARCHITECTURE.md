# SP Invisível — Architecture Rules

## Solution boundaries

The solution follows a feature-oriented DDD architecture. Each feature is an aggregate boundary and its related types remain grouped inside that feature.

### `RemoteTickets.Domain`

- Contains domain modeling only: entities, value objects, enums, domain events, domain rules, and domain abstractions that are intrinsically domain concepts.
- Must not reference another solution project.
- Must not reference infrastructure, application, presentation, ASP.NET Core, Entity Framework Core, Identity, logging infrastructure, HTTP, or persistence concerns.
- `Common` is reserved for domain types and abstractions genuinely shared by multiple domain features. It must not become a dumping ground.

### `RemoteTickets.Application`

- Contains use-case orchestration and contracts: commands, queries, handlers, requests, responses, events, validators, pipeline behaviors/middleware abstractions, and application services.
- May depend on `RemoteTickets.Domain`.
- Must depend on abstractions rather than infrastructure implementations.
- Must not contain EF Core `DbContext`, repository implementations, Identity implementations, HTTP transport concerns, or UI concerns.

### `RemoteTickets.Infrastructure`

- Contains concrete implementations of application/domain abstractions.
- Owns persistence, `DbContext`, repositories, Identity infrastructure, localization infrastructure, external integrations, and other technical concerns.
- May depend on `RemoteTickets.Application` and `RemoteTickets.Domain`.
- Infrastructure implementations must not leak into domain models or application contracts.

### `RemoteTickets.Composition`

- Contains composition-root extensions for `WebApplicationBuilder` and `WebApplication`.
- Exposes one complete pre-build service-registration/setup method and one complete HTTP-pipeline setup method.
- Owns dependency-injection composition and middleware/endpoint mapping orchestration.
- May reference ASP.NET Core framework types and the projects required to compose the application.
- Application/domain code must never depend on `RemoteTickets.Composition`.

### `RemoteTickets`

- Is the presentation/entry-point project and contains the Blazor Interactive UI and controllers.
- Receives presentation requests and translates them into application commands/queries.
- Consumes application contracts and never accesses repositories or `DbContext` directly.
- Maps application results to presentation responses/view models where required.
- Startup composition must use `RemoteTickets.Composition` rather than duplicating service-registration or pipeline configuration.

## Dependency direction

The intended dependency direction is:

`RemoteTickets` → `RemoteTickets.Composition` → `RemoteTickets.Infrastructure` → `RemoteTickets.Application` → `RemoteTickets.Domain`

`RemoteTickets.Application` may also reference `RemoteTickets.Domain` directly. `RemoteTickets.Domain` remains dependency-free.

Infrastructure is an implementation detail. Presentation must not bypass the application layer to access infrastructure services, repositories, or persistence.

## Feature organization

Each project is organized by feature first, not by technical type globally.

Preferred:

```text
RemoteTickets.Application/
  Identity/
    Abstractions/
    Commands/
    Handlers/
    Queries/
    Requests/
    Responses/
    Validators/
```

Avoid structures such as a single solution-wide `Services/`, `Repositories/`, `Handlers/`, or `Models/` directory that mixes unrelated features.

The feature name should represent the aggregate/use-case boundary. `Common` is allowed only when a type is truly shared by more than one feature.

## C# source conventions

- Target .NET 10.
- Enable nullable reference types.
- Use file-scoped namespaces in every C# source file.
- Do not use `using` directives in individual source files.
- Every C# project must contain a root-level `GlobalUsings.cs`, at the same directory level as its `.csproj`.
- `GlobalUsings.cs` is the single location for project-wide imports.
- Keep global imports minimal: only namespaces required by multiple source files belong there.
- Use explicit `using` aliases in `GlobalUsings.cs` when namespaces expose ambiguous type names.
- Prefer modern C#/.NET 10 language and BCL APIs when they improve correctness, clarity, performance, or maintainability.
- Prefer async APIs for I/O-bound operations and propagate `CancellationToken` through application and infrastructure boundaries.
- Avoid service-locator patterns and direct `IServiceProvider` resolution in application/domain code except for isolated infrastructure adapters whose responsibility is explicitly to bridge a third-party pipeline into the application abstraction.
- Do not introduce nullable suppression (`!`) without a concrete invariant that makes the value safe.

## XML documentation

Every public type and public member in every C# project, including test projects, must have XML documentation.

Documentation is part of the public API contract and must contain, when applicable:

- `<summary>` — what the type/member does and its responsibility.
- `<typeparam>` — every generic type parameter.
- `<param>` — every method, constructor, indexer, or delegate parameter.
- `<returns>` — the returned value for methods/functions that return a value or task result.
- `<exception>` — every exception that callers can reasonably encounter as part of the documented contract.
- `<remarks>` — important invariants, lifecycle requirements, security considerations, side effects, threading behavior, or usage constraints that are not appropriate for the summary.

Use `<inheritdoc />` when the inherited/interface documentation completely describes the public member and no additional contract information is necessary. Do not use empty documentation merely to silence an analyzer.

XML documentation must describe the behavior of the implementation rather than restating the member name. Public API changes must update their documentation in the same change.

The compiler XML documentation diagnostics are enabled in `.editorconfig`; malformed or incomplete parameter/type-parameter documentation must not be introduced.

## Dependency injection

- Register dependencies in `RemoteTickets.Composition`.
- Prefer constructor injection.
- Register abstractions against infrastructure implementations.
- Keep lifetimes intentional: singleton only for stateless/thread-safe shared services, scoped for request/unit-of-work services, transient for lightweight stateless services where appropriate.
- Do not register infrastructure types directly in presentation components/controllers when an application abstraction exists.

## Application flow

The normal request flow is:

`Blazor page/controller request` → `Command/Query` → `Handler` → `Application abstraction` → `Infrastructure implementation` → `Repository` → `DbContext` → `Entity/result` → `Application response` → `Presentation`

A handler should orchestrate a use case rather than become a persistence abstraction. Mapping between domain entities and application responses belongs at the application boundary unless the mapping is itself a domain concern.

## Domain rules

- Domain entities enforce their own invariants whenever practical.
- Domain models must not depend on UI, persistence, HTTP, serialization, or framework-specific infrastructure.
- Avoid anemic domain models when business invariants can naturally be expressed in the domain layer.
- Domain events represent meaningful domain occurrences; application/infrastructure concerns subscribe through appropriate abstractions.

## Testing and coverage

- Every production feature must have unit tests in `tests/RemoteTickets.UnitTests` unless a documented architectural reason makes a unit test inappropriate.
- Unit tests use xUnit v3, FluentAssertions, and `coverlet.collector`.
- Tests must exercise both success and failure paths, including meaningful branch conditions.
- The CI test job must collect line and branch coverage from Coverlet's XML output.
- Minimum required coverage is **80% lines and 80% branches** for the code included in the coverage report.
- Coverage thresholds are enforced by CI; a test run below either threshold fails the job.
- Coverage reports must be rendered as Markdown and appended to the GitHub Actions job summary so the agent can inspect the measured line and branch percentages.

## Global build enforcement

`Directory.Build.props` establishes the shared .NET 10 compiler/analyzer defaults for all projects beneath the repository root and generates XML documentation files.

`Directory.Build.targets` validates that every C# project has a root-level `GlobalUsings.cs` before compilation.

The repository `.editorconfig` enforces file-scoped namespaces, centralized import conventions, and XML documentation diagnostics for public APIs. Build-time analyzers are enabled so code-style and analyzer feedback is visible during builds.
