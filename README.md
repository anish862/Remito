# Remito

**Compare Oman's exchange rates. Pay through the exchange you choose.**

Remito is an Android app that lets users compare exchange rates from licensed exchange houses in Oman, pick one, and get redirected to that exchange's own app or payment gateway to complete the transfer. Phase 1 targets the Oman to India (OMR to INR) corridor.

> **Remito never holds, processes or moves customer money.** It is a comparison and referral layer only. Transactions are completed on the chosen licensed exchange's own app or gateway.

## Contents

- [Product rules](#product-rules)
- [Features (Phase 1 MVP)](#features-phase-1-mvp)
- [Architecture](#architecture)
- [Tech stack](#tech-stack)
- [Repository structure](#repository-structure)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Testing](#testing)
- [CI/CD and environments](#cicd-and-environments)
- [Engineering standards](#engineering-standards)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [Contacts](#contacts)

## Product rules

These apply to every feature and must not be broken:

1. No fund handling of any kind. Remito compares and redirects only.
2. Fees and amount received are shown only when an exchange supplies fee data. Otherwise the UI shows "Fee not provided".
3. Sponsored or featured listings are always labelled and never affect which exchange is marked "Best rate".
4. Every rate shows a last-updated time, with a stale warning when data is old.
5. Compare and redirect screens show: *"Remito does not process payments. Transactions are completed on the chosen licensed exchange."*
6. No personal data in redirect URLs, analytics events or logs.

## Features (Phase 1 MVP)

- Dashboard of major currency rates with 24h change and last-updated time
- Currency converter using live rates (works offline with last known rate, clearly labelled)
- Rate history charts: 1D, 1W, 1M, 6M, 1Y
- Side-by-side exchange comparison, best-rate sorting, fees and amount received (where available)
- One-tap redirect to the chosen exchange's app or gateway, with click tracking
- Favourites, rate alerts with push notifications, FX news, search
- Admin portal: vendors, fees, rate overrides, ads, referral and commission reports
- Advertising and featured listings (labelled, consent-aware)

Phase 2 (Year 2): iOS and web, advanced analytics, AI-based rate prediction, multi-language, crypto and metals rates, API products.

## Architecture

```
Android app (Kotlin, Compose)
        |
   HTTPS / JWT
        |
ASP.NET Core 8 API  ---- Redis (hot rates, rate limits)
        |
   Azure SQL  <---- Hangfire workers (rate ingestion, alerts, reconciliation)
        |
Partner feeds / market-rate API / FCM / partner postbacks
```

Backend is a modular monolith with clean-architecture layers (Domain, Application, Infrastructure, Api). Dependency rules are enforced by an architecture test.

## Tech stack

| Area | Technology |
|---|---|
| Android | Kotlin, Jetpack Compose (Material 3), MVVM + Clean Architecture, Hilt, Retrofit, Room, WorkManager, FCM, Coroutines/Flow |
| Backend | ASP.NET Core 8, EF Core 8, SQL Server (Azure SQL), Redis, Hangfire, Polly, Serilog |
| Admin | ASP.NET Core (Razor Pages or Blazor Server) |
| Infra | Azure (App Service, Azure SQL, Redis, Key Vault, Blob, App Insights), Bicep |
| CI/CD | GitHub Actions |
| Testing | xUnit, FluentAssertions, Testcontainers, JUnit5, MockK, Turbine, Compose UI tests, k6 |

## Repository structure

```
/backend   Remito.sln (Api, Application, Domain, Infrastructure, Workers, tests)
/android   Android project (app, core:*, feature:*)
/admin     Step 14 placeholder
/infra     Azure Bicep modules and dev/staging/prod parameters
/docs      architecture.md, adr/, progress.md
```

## Getting started

### Prerequisites

- .NET 8 SDK and runtime
- Docker Desktop or Docker Engine with Compose
- Android Studio, JDK 17, and Android SDK Platform 36

### 1. Start local dependencies

Set a local-only SQL Server password and start the backend dependencies:

```bash
export MSSQL_SA_PASSWORD='Choose_a_local_password_123!'
docker compose up -d
dotnet user-secrets set --project backend/Remito.Api "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=Remito;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
dotnet user-secrets set --project backend/Remito.Api "Redis:ConnectionString" "localhost:6379"
```

The API requires SQL Server configuration at startup. OTP authentication additionally requires Redis; in Development, the console sender reports delivery without writing OTPs or destinations to logs. Configure a real SMS/email sender outside local development.

### 2. Build and test the backend

```bash
dotnet build backend/Remito.sln
dotnet test backend/Remito.sln
```

Run the API with `dotnet run --project backend/Remito.Api`. Open `/health` for health status and `/swagger` for the non-production API docs. For SQL/Redis use, configure local user-secrets; never commit credentials.

### 3. Build and run Android

```bash
cd android
./gradlew assembleDebug testDebugUnitTest
./gradlew ktlintCheck detekt
```

Open `/android` in Android Studio, create or select an API 26+ emulator, and run the `app` configuration. The shell currently uses placeholder screens and does not make API calls.

### 4. Admin and infrastructure

`/admin` remains a placeholder. Infrastructure modules, environment parameters, pipeline setup, and rollback instructions are documented in [`docs/infra.md`](docs/infra.md). Azure names and GitHub OIDC/environment configuration must be set for your subscription before deploying.

## Configuration

Never commit secrets. Use user-secrets locally and Key Vault in Azure.

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings:Default` | Backend | SQL Server connection |
| `Redis:ConnectionString` | Backend | Required by OTP rate limiting |
| `Auth:Issuer`, `Auth:Audience`, `Auth:SigningKey` | Backend | JWT issuer/audience and signing key; use Key Vault in cloud |
| `Auth:OtpHashKey` | Backend | HMAC key for destination and OTP hashes; use Key Vault in cloud |
| `Auth:RefreshTokenLifetimeDays` | Backend | Rotating refresh token lifetime |
| `Auth:TermsVersion`, `Auth:PrivacyVersion` | Backend | Current consent document versions |
| `RateProviders:*` | Backend | Market-rate API key and per-exchange feed settings |
| `Firebase:ServiceAccount` | Backend | FCM push |
| `IOtpSender` implementation | Backend | Console sender in Development; configure a production SMS/email sender |
| `PartnerPostback:Secrets` | Backend | HMAC secrets per exchange |
| `API_BASE_URL` | Android `local.properties` / flavour | Per environment |
| `google-services.json` | Android | Per flavour, not committed |

## Testing

```bash
# Backend
cd backend
dotnet test

# Android
cd android
./gradlew testDebugUnitTest
./gradlew connectedDebugAndroidTest
./gradlew ktlintCheck detekt lint
```

Quality priorities, in order: rate accuracy and freshness, redirect integrity, security and privacy, reliability and performance, accessibility.

Launch exit criteria: no open critical or high bugs, crash-free sessions above 99.5%, API P95 below 400 ms, cold start below 2.5 s on a mid-range device, VAPT high/critical findings closed.

## CI/CD and environments

| Environment | Deploys on | Notes |
|---|---|---|
| dev | merge to `main` | Auto; slot health check and swap |
| staging | version tag | Slot health check and swap |
| prod | version tag, after staging | GitHub environment approval; slot health check and swap |

Pipelines run backend build, unit and Testcontainers integration tests, CodeQL and dependency scans; Android lint, ktlint, detekt, unit tests, and debug APK build; and Bicep build and PR what-if. See [`docs/infra.md`](docs/infra.md) for required GitHub environment settings and branch protection.

## Engineering standards

- Money and rates: `decimal` (C#) and `BigDecimal` (Kotlin), never floating point. ISO 4217 currency codes. UTC timestamps.
- API under `/api/v1`, OpenAPI-first, RFC 7807 errors.
- All UI strings in resources (i18n and RTL ready). Accessibility: content descriptions, 48dp targets, AA contrast.
- Conventional commits, small PRs, tests with every feature.
- Architecture decisions recorded in `/docs/adr`.

## Roadmap

| Sprint | Goal |
|---|---|
| S0-S1 | Foundations, design, API contract, skeletons |
| S2 | Rates core, dashboard, convert |
| S3 | Comparison and redirect |
| S4 | History, favourites, alerts, push, news, admin |
| S5 | Ads, analytics, referral reconciliation, VAPT, accessibility |
| S6 | Closed beta, Play Store staged rollout |

Target: launch end of Month 3 with the full team, Month 6 with the core team only. See `/docs/progress.md` for current status.

## Contributing

1. Branch from `main` (`feature/<short-name>`).
2. Keep PRs small and include tests.
3. Run lint and tests locally before opening a PR.
4. PR description must list assumptions and open questions.
5. At least one review from CODEOWNERS is required.

## Contacts

- Remito India, Technopark, Thiruvananthapuram (Kerala Startup Mission)
- Remito Global, Muscat, Sultanate of Oman
- Contact: `[name]` · `[email]` · `[phone]` · `[website]`

## Licence

`[To be decided]`