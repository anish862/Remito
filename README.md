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

### Rates pipeline

The worker registers a one-minute Hangfire rate-ingestion job and an hourly history roll-up job when `ConnectionStrings:Default` is configured. Apply EF migrations before starting it; run the Development worker locally with `dotnet run --project backend/Remito.Workers` to ingest the recorded fixture into local SQL and Redis (or the development in-memory cache when Redis is not configured). `Rates:Market:Endpoint` enables the licensed FX adapter; `Rates:Exchanges:Feeds` accepts one HTTPS feed definition per exchange with `Name`, `SourceId`, `Endpoint`, `Strategy` (`json`, `csv`, or `html`), `StalenessMinutes`, and optional request `Headers`. JSON and table/CSV inputs use `pair`, `bid`, `ask`, and optional `mid` fields/columns. Supply `Rates:Market:ApiKey` and exchange credentials through the host configuration provider backed by Key Vault; the market adapter also accepts the existing `RateProviders:ApiKey` setting. Use worker user-secrets locally. Credentials are never logged.

Configure `Rates:Guards:MaximumVariancePercent` and per-pair `PairBounds`. Quotes outside the source freshness window or bounds are not published; large changes are persisted in `RateQuarantines` and logged as alert events. Provider outages retain the last good rate and mark SQL and Redis values stale. Configure an App Insights connection string for the worker to publish `RateFreshnessSeconds`, `RateFetchDurationMilliseconds`, `RateSourceFetchSucceeded`, and `RateQuarantineCount` metrics.

Development workers load the recorded fixture in `backend/Remito.Workers/Fixtures/rates.json` and require only the local SQL connection. For an accuracy audit, export stored latest values and a second provider snapshot as CSV files with `pair,mid` columns, then run:

```bash
python3 backend/scripts/rate_accuracy_audit.py \
  --stored stored-rates.csv \
  --comparison second-source-rates.csv \
  --threshold-percent 1
```

The audit reports missing pairs and above-threshold variance and exits non-zero for either condition.

The API exposes `GET /api/v1/rates/latest?base=OMR&symbols=INR,AED`, `GET /api/v1/rates/history?pair=OMR-INR&range=1D`, and `POST /api/v1/convert` with `{"amount":10,"from":"OMR","to":"INR"}`. Latest rates use the aggregate Redis cache and fall back to SQL; stale values remain available with `isStale: true`. Latest responses include an ETag and a 15-second public cache lifetime. Conversion accepts positive amounts up to 1 trillion, rejects same-currency conversions, uses USD as the cross-rate pivot when needed, and rounds to the destination currency precision using midpoint-to-even (banker's) rounding.

The exchange comparison API exposes `GET /api/v1/exchanges` and `GET /api/v1/compare?from=OMR&to=INR&amount=10`. The exchanges listing defaults to the OMR-INR corridor and 10 OMR; both endpoints accept `from`, `to`, `amount`, `sort=best|rate|name`, `hasApp`, `search`, `page`, and `pageSize` (maximum 100). Only active non-placeholder exchanges with a quote are returned; stale quotes remain visible with `isStale: true`. Fees are selected from the matching amount band and calculated as flat fee plus percentage fee (percentage expressed in percent units); fees are rounded to source-currency precision and amount received to destination-currency precision using midpoint-to-even. Offers with known fees rank by amount received, followed by rate; unknown fees are clearly disclosed and ranked by rate. Sponsored offers are returned in a separately labelled slot and do not receive any ranking or `isBestRate` advantage. Comparison results are cached for 60 seconds per corridor and exact amount. Responses include the payment disclaimer in metadata.

To measure local cached latest-rate latency, install [k6](https://grafana.com/docs/k6/latest/set-up/install-k6/) and run the Development API and rate worker as described below. The test warms the route under a 20-virtual-user load and fails if P95 reaches 200 ms:

```bash
k6 run backend/scripts/rates-cache-load.js
```

Use `API_BASE_URL` to target another local API URL. k6 prints the measured `http_req_duration` P95 in its summary. Record the date, machine, runtime, and P95 next to the task's progress entry when benchmarking a new environment; a local result does not predict production latency.

### Local installation

From the repository root, install the .NET 8 SDK and Docker, then start SQL Server and Redis. Keep the local SQL password out of source control:

```bash
export MSSQL_SA_PASSWORD='Choose_a_local_password_123!'
docker compose up -d
dotnet user-secrets set --project backend/Remito.Api "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=Remito;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
dotnet user-secrets set --project backend/Remito.Api "Redis:ConnectionString" "localhost:6379"
dotnet user-secrets set --project backend/Remito.Workers "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=Remito;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
dotnet user-secrets set --project backend/Remito.Workers "Redis:ConnectionString" "localhost:6379"
```

Apply the database migrations once (`dotnet ef` 8.0.22 is required):

```bash
dotnet tool install --global dotnet-ef --version 8.0.22
dotnet ef database update --project backend/Remito.Infrastructure --startup-project backend/Remito.Api
```

Run the API and worker in separate terminals from the repository root. Development configuration enables the recorded fixture rates; Swagger is available at `http://localhost:5191/swagger`.

```bash
dotnet run --project backend/Remito.Api
dotnet run --project backend/Remito.Workers
```

The local worker applies recurring ingestion and history roll-up jobs. To check the endpoint contracts and implementation, run:

```bash
DOTNET_ROLL_FORWARD=Major dotnet test backend/Remito.UnitTests --filter FullyQualifiedName~RateQueryServiceTests
DOTNET_ROLL_FORWARD=Major dotnet test backend/Remito.IntegrationTests --filter FullyQualifiedName~Swagger_includes_passwordless_auth_and_bearer_security
```

### Production installation

Production is deployed to Azure App Service using the Bicep environment definitions and GitHub Actions; do not install the API or worker manually on a developer workstation. Follow [`docs/infra.md`](docs/infra.md) to configure GitHub OIDC, the Azure subscription and environment approvals, provision the production SQL/Redis/Key Vault/App Insights resources, and deploy `infra/environments/prod.bicepparam`. Before enabling traffic:

1. Add provider credentials and the JWT/OTP signing secrets to the private Key Vault using the approved secret-provisioning process; grant the API and worker managed identities only the required Key Vault, SQL, and Redis access. Never put secret values in app settings or command history.
2. Configure production SQL and Redis connection settings as Key Vault references, production `Rates:Market` and `Rates:Exchanges` provider settings, App Insights, and the production sender for SMS/email. Keep the Development fixture provider disabled.
3. Run the database migration workflow with the designated migration identity and required DDL role, then deploy the API and worker using the protected production environment. Verify `/health`, `/swagger` in a non-production slot, latest-rate freshness, history, conversion, and App Insights rate metrics before the approved slot swap.
4. Run `backend/scripts/rates-cache-load.js` against the production-like staging slot with `API_BASE_URL` set to that slot. Capture the k6 summary (especially P95), machine/load settings, and date in the release record. Do not treat a developer-local benchmark as a production performance guarantee.

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

## Current implementation progress

| Area | Status | Delivered / remaining |
|---|---|---|
| Passwordless phone/email OTP and token lifecycle | Implemented | OTP abuse protections, refresh-token rotation/reuse detection, audit logging, and account/device/consent routes are in the backend. |
| Rate ingestion pipeline | Implemented | Configurable providers, guards/quarantine, SQL history, Redis hot cache, Hangfire jobs, freshness metrics, fixtures, and accuracy audit script are in place. |
| Latest rates, history, and conversion API | Implemented | Redis-first reads with SQL fallback, stale flags, ETag/cache headers, 200-point history downsampling, USD cross-rates, ISO currency precision, and banker's rounding. |
| Exchange listing and comparison API | Implemented | Active exchange quotes, amount-band fee calculation, deterministic ranking/filtering, stale warnings, labelled sponsored slot, disclaimer, and 60-second corridor/amount cache are covered by fixture-driven tests. |
| Endpoint correctness and performance QA | In progress | Exchange comparison hand-calculated fixture tests and OpenAPI contract checks pass. The referenced Q3 QA prompts are not included in this checkout; run them when available. Record the k6 P95 after running the local or staging load test; target is under 200 ms from cache. |
| Production rollout | Remaining | Provision environment secrets/provider credentials, run the protected migration/deployment workflow, and capture staging/production load evidence as described above. |
| Product integrations | Remaining | Android UI/API wiring and the admin portal are not delivered by these backend tasks; validate the licensed provider contracts and production alert recipients before launch. |

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