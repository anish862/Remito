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
- Local SQL Server and Redis compose services
- Repository documentation and contribution templates

## Next

- Complete admin and Azure Bicep work in their planned steps
- Run Android instrumentation tests on an emulator; finish screenshot matrices, TalkBack, contrast, and large-font QA
- Configure real staging/production API URLs, certificate pins, and release signing; verify OTP delivery against the development sender
- Add explicit Room migration execution tests and complete fake-API repository/interceptor/UI test coverage
- Wire auth/data repositories into remaining feature modules (alerts, profile) and complete generated OpenAPI client output
- Run the redirect one-tap/one-event and no-personal-data-in-URL integrity checklist on device; connect release analytics provider