# Abstractions (Abstract Types and Interfaces)

Source: [.NET Framework Design Guidelines — Abstractions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces)
(From *Framework Design Guidelines: Conventions, Idioms, and Patterns for Reusable .NET Libraries, 2nd Edition*, Cwalina & Abrams.)

## What an abstraction is

A type that describes a contract without fully implementing it — typically an abstract class or an interface, paired with documented semantics that implementers must follow. Examples: `Stream`, `IEnumerable<T>`, `Object`. Frameworks are extended by writing a concrete type that satisfies the contract and handing it to APIs that consume the abstraction.

## Why abstractions are hard to get right

- Designing a durable, useful abstraction is difficult — the hard part is landing on the *right* member set: too many members and it becomes hard or impossible to implement; too few and it's useless for interesting scenarios.
- Too many abstractions in a framework hurts usability — understanding one abstraction usually requires understanding how it fits with concrete implementations and the APIs that operate on it, and abstraction/member names tend to be inherently abstract (and thus cryptic) without that broader context.

## Why they're worth it

- Abstractions enable extensibility other mechanisms can't match, and underpin architectural patterns like plug-ins, inversion of control (IoC), and pipelines.
- They're central to testability — good abstractions let heavy dependencies be stubbed out for unit tests.

## The rules

- ❌ **DO NOT** provide an abstraction unless it's proven by developing several concrete implementations and the APIs that consume it.
- ✔️ **DO** choose carefully between an abstract class and an interface when designing an abstraction.
- ✔️ **CONSIDER** providing reference tests for concrete implementations, so users can verify their implementation satisfies the contract correctly.

## Relevance to this repo

This is the basis for [[AGENTS.md]]'s "no repository interfaces" decision: `Anywhere.Models` has exactly one concrete data store (SQLite via EF Core), no second implementation is planned, and the ACP layer is already tested against a real fake agent rather than mocks — so per the DO NOT rule above, an `IProfileRepository`-style abstraction isn't justified. Revisit only if a genuine second backing store or a concrete need for a test fake materializes.
