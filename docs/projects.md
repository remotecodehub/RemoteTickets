# Project Guide

| Project | Path | Responsibility | XML documentation in Release |
| --- | --- | --- | --- |
| RemoteTickets.Domain | `src/server/RemoteTickets.Domain/RemoteTickets.Domain.csproj` | Domain model and domain abstractions | Yes |
| RemoteTickets.Application | `src/server/RemoteTickets.Application/RemoteTickets.Application.csproj` | Use cases, contracts, handlers, validation | Yes |
| RemoteTickets.Infrastructure | `src/server/RemoteTickets.Infrastructure/RemoteTickets.Infrastructure.csproj` | Persistence, Identity-like services, JWT, integrations | Yes |
| RemoteTickets.Composition | `src/server/RemoteTickets.Composition/RemoteTickets.Composition.csproj` | Dependency injection and application composition | Yes |
| RemoteTickets | `src/server/RemoteTickets/RemoteTickets.csproj` | Blazor Interactive Server presentation and HTTP controllers | Yes |
| RemoteTickets.UnitTests | `tests/server/RemoteTickets.Unittests/server/RemoteTickets.UnitTests.csproj` | Unit and infrastructure-backed tests | No file publication |

## Project-specific rules

### Domain

Keep the project dependency-free from the other solution projects and infrastructure frameworks. Domain abstractions must express business concepts rather than persistence mechanics.

### Application

Keep use-case orchestration and contracts independent of infrastructure implementations. Requests, handlers, validators, and application abstractions belong here. Infrastructure types must not leak into application contracts.

### Infrastructure

Implement application and domain abstractions here. Persistence and Identity-like behavior are infrastructure concerns. Keep technical details out of application and domain models.

### Composition

Centralize dependency registration and HTTP pipeline configuration. The entry-point project should call these extensions instead of duplicating startup configuration.

### Presentation

Controllers and Blazor components translate presentation input into application requests. Do not access `DbContext`, repositories, or other infrastructure services directly. if an application abstraction isn't available to access the feature, implement one in the `Common` feature aggregate or in the appropriated feature.

### Tests

Use the actual production implementation when the test validates production behavior. Test doubles are appropriate for handler delegation and isolated application orchestration tests. Test XML comments are required, but the shared Release documentation target excludes this project from publishing XML files.
