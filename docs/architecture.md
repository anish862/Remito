# Architecture

## System boundaries

Remito is a comparison and referral service. It never holds, processes, or moves customer money. A selected offer redirects the customer to the licensed exchange's own application or payment gateway.

## Backend

The backend is a modular monolith with inward dependencies:

```text
Remito.Domain
    ^
Remito.Application
    ^                 ^
Remito.Infrastructure  Remito.Api
                       ^
                   Remito.Workers
```

Domain has no project or package dependencies. Application owns use cases, DTOs, and interfaces and depends only on Domain and framework DI abstractions. Infrastructure implements Application interfaces and contains EF Core, Redis, providers, and job integrations. API and Workers are composition roots. NetArchTest rules run with the unit tests.

The API exposes versioned JSON routes under `/api/v1`, RFC 7807 errors, `/health`, and non-production Swagger. Health is independent of optional infrastructure configuration.

## Android

The Android app is a Compose shell with five bottom-navigation destinations. Features depend on shared core modules; feature screens do not own application money movement. UI strings live in Android resources, and currency amounts/rates must use `BigDecimal`.

## Data and operational rules

- Store currency codes as ISO 4217 codes and timestamps in UTC.
- Display fees and amount received only when supplied by an exchange; otherwise display `Fee not provided`.
- Sponsored listings are visibly labelled and never affect the best-rate calculation.
- Every rate view includes its update time and a stale-data warning.
- Do not place personal data in logs, analytics, or redirect URLs.
- Compare and redirect screens show: `Remito does not process payments. Transactions are completed on the chosen licensed exchange.`