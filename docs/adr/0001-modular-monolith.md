# ADR 0001: Backend modular monolith

- Status: Accepted
- Date: 2026-10-20

## Context

The first release needs a small deployable API and background processing without weakening domain boundaries or adding distributed-system operations.

## Decision

Use one ASP.NET Core solution with Domain, Application, Infrastructure, API, Workers, and test projects. Enforce allowed assembly dependencies with NetArchTest.

## Consequences

The API and worker host can deploy independently while sharing application and infrastructure code. Layer boundaries remain testable; deployment complexity stays low for the initial product.