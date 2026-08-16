# Remote Tickets Architecture

## Solution layers

The solution is a feature-oriented DDD application with the following dependency direction:

`RemoteTickets` → `RemoteTickets.Composition` → `RemoteTickets.Infrastructure` → `RemoteTickets.Application` → `RemoteTickets.Domain`

`RemoteTickets.Application` may reference `RemoteTickets.Domain` directly. `RemoteTickets.Domain` must remain independent of the other solution projects.

### `src/server/RemoteTickets.Domain`

Contains domain entities, value objects, domain events, rules, and domain abstractions. It must not reference ASP.NET Core, Entity Framework Core, Identity, HTTP, UI, logging infrastructure, or persistence implementations.

### `src/server/RemoteTickets.Application`

Contains use-case contracts and orchestration: requests, responses, handlers, validators, application abstractions, and pipeline behavior. It depends on domain concepts and abstractions, not infrastructure implementations.

### `src/server/RemoteTickets.Infrastructure`

Contains technical implementations: Entity Framework Core persistence, Identity-like authentication infrastructure, JWT services, external integrations, and concrete implementations of application abstractions. May
contain behaviors related to infrastructure functionalities.

### `src/server/RemoteTickets.Composition`

Owns dependency injection and HTTP/application composition. It provides the application service-registration extension and the HTTP pipeline extension. Application and domain code must never depend on this project.

### `src/server/RemoteTickets`

Is the presentation and entry-point project. It contains Blazor Interactive Server UI and controllers. Controllers translate HTTP requests into application requests and must not access `DbContext` or repositories directly.

## Feature boundaries

Features are grouped by business/use-case boundary. `Common` is reserved for types genuinely shared across multiple features. Avoid global technical folders that mix unrelated features.

## Identity boundary

Identity behavior is implemented by `RemoteTickets.Infrastructure.Identity.Services.IdentityService` and related infrastructure types. Application contracts expose identity operations through `IIdentityService`. Tests must target this implementation contract and its actual Identity configuration.

Two-factor authentication uses the Identity authenticator token provider and an authenticator shared key. A TOTP code is generated from that key by the authenticator/client side and passed back to the service for validation.

## Persistence

`RemoteTicketsDbContext` derives from the Identity EF Core context and owns persistence configuration. Types implementing `ISoftDeletable` receive a query filter and deleted entities are converted to updates by the save pipeline.

## Composition

Dependency registration belongs in `RemoteTickets.Composition`. Presentation startup calls the composition extension instead of duplicating registrations. Application handlers depend on application abstractions and infrastructure supplies their implementations.

## Tests

`tests/server/RemoteTickets.UnitTests` contains unit and infrastructure-backed tests for the production layers. It uses EF Core InMemory for persistence-focused scenarios and the real Identity implementation for identity behavior. Tests are excluded from Release XML documentation publication but are still required to contain XML documentation for public types and members.

## Build documentation

`Directory.Build.props` establishes common .NET 10 settings. `Directory.Build.targets` validates `GlobalUsings.cs` and defines `GenerateXmlDocumentation` for Release production builds. Generated XML files are copied to `docs-gen/` at repository root.

The `docs/` directory is reserved for human- and agent-authored documentation and must never be used as the generated XML output directory.
