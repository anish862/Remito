# Foundation progress

## Complete

- .NET 8 solution and clean-architecture project graph
- API health, versioning, RFC 7807 handling, logging, and non-production Swagger
- NetArchTest architecture rules and HTTP health test
- Android modular project, Compose shell, Material 3 theme, and Hilt wiring
- Android OTP entry/session gate and encrypted token persistence
- Android Retrofit/OkHttp rate access, serialized 401 refresh, RFC 7807 mapping, Room cache, and offline-first rate repository
- Dashboard, converter, and history Compose screens with ViewModels and cached-data states
- Local SQL Server and Redis compose services
- Repository documentation and contribution templates

## Next

- Add domain entities and use cases for exchange rates and comparison
- Add database migrations and provider ingestion contracts
- Implement exchange comparison UX and safe redirect flow
- Complete admin and Azure Bicep work in their planned steps
- Run Android lint/detekt/unit/instrumentation tests on an Android SDK and emulator; complete screenshot, TalkBack, contrast, and large-font QA
- Configure real staging/production API URLs, certificate pins, and release signing; verify OTP delivery against the development sender
- Add explicit Room schema migration tests and complete fake-API repository/interceptor/ViewModel test coverage
- Wire auth/data repositories into remaining feature modules (comparison, alerts, profile) and complete generated OpenAPI client output