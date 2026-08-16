# C# Coding Style

## Language

- Target .NET 10.
- Use current C# language features when they improve clarity and correctness.
- Enable nullable reference types.
- Prefer `async` APIs for I/O and propagate `CancellationToken` through application and infrastructure boundaries.
- Prefer primary constructors always when possible
- Avoid unnecessary nullable suppression (`!`).

## Namespaces and imports

- Use file-scoped namespaces.
- Individual C# source files must not contain `using` directives.
- Project-wide imports belong in the root `GlobalUsings.cs`.
- Keep global imports minimal and use aliases when necessary to resolve ambiguous types.
- Do not add unnecessary imports.

## API design

- Prefer constructor injection.
- Keep public APIs small and intentional.
- Use records for immutable request/response contracts where appropriate.
- Keep domain invariants in the domain model whenever practical.
- Keep transport concerns in presentation and technical concerns in infrastructure.
- Controllers should annotate all possible results with response object type and status code of result.

## XML documentation

Public APIs must document their contract. Use `<summary>`, `<param>`, `<typeparam>`, `<returns>`, `<exception>`, and `<remarks>` when applicable. Documentation should explain behavior, constraints, and side effects rather than repeat the identifier name.

Use `<inheritdoc />` for complete inherited/interface contracts. Do not use placeholder text or empty XML elements.

## Formatting

Follow the repository `.editorconfig`: four-space indentation for C#, LF line endings, file-scoped namespaces, braces for control flow, and centralized imports. Keep code readable rather than optimizing for compressed one-line expressions.

## Dependency injection

Register dependencies in `RemoteTickets.Composition`. Prefer abstractions at application boundaries. Choose service lifetimes intentionally: singleton for safe shared state/stateless thread-safe services, scoped for request/unit-of-work services, and transient for lightweight stateless services when justified.

## Tests

Name tests by behavior and expected outcome. Prefer one behavioral assertion group per scenario. Exercise both success and meaningful failure branches. Use the real production implementation when the purpose of the test is to validate that implementation.
