# Foundation progress

## Complete

- .NET 8 solution and clean-architecture project graph
- API health, versioning, RFC 7807 handling, logging, and non-production Swagger
- NetArchTest architecture rules and HTTP health test
- Android modular project, Compose shell, Material 3 theme, and Hilt wiring
- Android OTP entry/session gate and encrypted token persistence
- Android Retrofit/OkHttp rate access, serialized 401 refresh, RFC 7807 mapping, Room cache, and offline-first rate repository
- Dashboard, converter, and history Compose screens with ViewModels and cached-data states
- Exchange comparison, offer details, redirect confirmation, app/store/gateway fallback, and Room-cached offers
- Redirect event tracking seam with PII-free event names and server-allow-listed destination URLs
- Android currency/exchange favourites, cached quick rates, rate-alert management, cached news, profile/consent settings, push channels/deep links, and constrained background refresh
- Backend favourites, alerts, and device-list/remove API endpoints wired to existing services; authenticated notification preferences API
- Local SQL Server and Redis compose services
- Repository documentation and contribution templates

## Delivered in the current admin/ads/analytics increment

- Added role-protected scheduled ad campaign listing/creation with HTTPS creative URL validation and audit logging.
- Added authenticated contextual slot selection for the four supported slots, a rolling per-user impression cap, explicit `Ad` label, click/impression recording, and a server-side configuration switch.
- Added the fixed analytics taxonomy endpoint and UUIDv4 installation-id hashing before App Insights ingestion; added one typed Android tracker with a locally persisted random installation UUID.
- Added API rate limiting, default-deny CORS with exact-origin configuration, security headers, and an RBAC role/action policy matrix test.
- Verification: full backend unit suite passes (77/77); the focused RBAC role/action matrix, frequency-cap threshold cases, and analytics taxonomy/UUID hashing tests pass (17/17). RBAC coverage checks all 4 roles against 12 protected admin actions (48 allow/deny checks). SQL-backed ad selection, cap enforcement, campaign creation/listing, and audit writes pass (1/1 integration test). Android network tests, dev Kotlin compilation, and ktlint pass with JDK 21/Android SDK configured. The backend solution builds without warnings.

### RBAC QA gate

Results from `AdminRbacMatrixTests`:

| Role | Allowed admin actions | Denied admin actions |
|---|---|---|
| Admin | All 12 protected actions | None |
| PartnerManager | 6 `PartnerManagement` actions and 2 `ReadOnly` actions | `AdsManagement` and `AdminOnly` |
| AdsManager | Campaign creation and 2 `ReadOnly` actions | `PartnerManagement` and `AdminOnly` |
| ReadOnly | Campaign listing and audit-log viewing | All write/management actions |

The test checks every role against every action policy (48 assertions); all passed. CSV-validation and fee-upload tests are still pending because the fee upload workflow is not implemented.

This is a partial increment, not completion of the requested programme. There is still no browser admin portal, fee CSV processing, vendor workflow, rate override, reporting/export, creative Blob upload, targeting/budget, Android ad renderer, analytics screen wiring/funnels/dashboards, Bicep alert completion, or security threat model/VAPT scope.

## Next

- Implement the Razor Pages/Blazor portal and remaining admin workflows, including strict validation, upload scanning, masked lookups, CSV exports, and audit viewer; add role/action and CSV validation tests.
- Complete ad campaign budgets/targeting/Blob creatives and consent-aware targeting only after consent semantics are defined; wire Android display/events behind a remotely refreshable kill switch and keep ads off the redirect confirmation screen.
- Wire analytics tracking at Android event sources, build funnel queries and App Insights dashboards, and finish Bicep alert rules (including push and crash-free signals).
- Complete MASVS L1/selected L2 verification, secret rotation, dependency/container scans, redirect SSRF/open-redirect tests, HMAC rotation, STRIDE threat model review, VAPT scope, and Q8 security test plan.
- Complete Azure Bicep work in the planned steps.
- Run Android instrumentation tests on an emulator; finish screenshot matrices, TalkBack, contrast, and large-font QA
- Configure real staging/production API URLs, certificate pins, and release signing; verify OTP delivery against the development sender
- Add explicit Room migration execution tests and complete fake-API repository/interceptor/UI test coverage
- Complete generated OpenAPI client output and add broader Android repository/ViewModel tests
- Run the redirect one-tap/one-event and no-personal-data-in-URL integrity checklist on device; connect release analytics provider
- Configure Firebase client/admin credentials and public legal URLs in each environment; validate real push delivery, permission denial, battery saver, and account deletion on physical devices