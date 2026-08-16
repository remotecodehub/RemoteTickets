# Agent Instructions

## Scope

This repository targets .NET 10 and follows the architecture described in `docs/architecture.md`. Treat repository documentation as part of the engineering contract.

## Before changing code

1. Read `docs/architecture.md`.
2. Read `docs/coding-style.md`.
3. Inspect the target project `.csproj` and its `GlobalUsings.cs` before adding code.
4. Keep changes inside the appropriate architectural boundary.
5. Update XML documentation whenever a public API changes.

## C# documentation rules

Every public type and public member in `src/` and `tests/` requires XML documentation. Document all applicable elements:

- `summary`: responsibility and behavior, not a restatement of the name.
- `typeparam`: every generic type parameter.
- `param`: every method, constructor, indexer, and delegate parameter.
- `returns`: every non-void return value, including task results.
- `exception`: exceptions that callers can reasonably encounter as part of the contract.
- `remarks`: invariants, side effects, security, lifecycle, threading, or other important constraints.

Use `<inheritdoc />` only when inherited/interface documentation completely describes the member. Never add empty documentation solely to suppress an analyzer.

Private implementation details do not require XML documentation unless their contract is non-obvious or the codebase already documents them for clarity.

## Imports and project structure

- Do not add `using` directives to individual C# files.
- Put project-wide imports in the root-level `GlobalUsings.cs`.
- Use file-scoped namespaces.
- Organize production code by feature first.
- Do not create generic dumping-ground folders such as solution-wide `Services`, `Models`, or `Repositories`.

## Build and documentation output

Release builds of production projects generate XML API documentation. The shared `Directory.Build.targets` copies those files to the repository-root `docs-gen/` directory.

The test project is deliberately excluded from XML documentation file publication. Test source code still follows the same XML documentation rules.

Do not place hand-written agent documentation in `docs-gen/`. That directory is generated output. Hand-written instructions belong under `docs/`.

## Tests

Tests use xUnit v3 and FluentAssertions. Preserve the existing test style and test the behavior of the actual implementation. Do not replace real infrastructure with unrelated abstractions merely to make a test pass.

For authentication tests, configure Identity-like behavior exactly as implemented by `RemoteTickets.Infrastructure`; do not assume ASP.NET Core Identity API endpoints are the implementation under test.

## Validation

For code changes, validate the narrowest relevant build first and then the solution build when practical. Release documentation generation should be verified with a Release build of production projects. Do not commit generated `docs-gen` files unless repository policy explicitly changes to version them.
