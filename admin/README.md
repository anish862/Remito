# Admin portal

## Current state

There is no browser-based Razor Pages or Blazor admin portal yet. The API has role policies (`Admin`, `PartnerManager`, `AdsManager`, `ReadOnly`) and a partial campaign-management surface:

- `GET /api/v1/admin/ad-campaigns` is available to all four admin roles.
- `POST /api/v1/admin/ad-campaigns` is available to `Admin` and `AdsManager`; it creates one scheduled campaign and HTTPS creative URL, then writes an `AuditLog` entry.
- Ad selection and impression/click event endpoints are documented in the root README.

All other exchange administration, fee uploads, rate overrides, featured listings, news editing, exports, privacy-masked lookup, and audit-log browsing endpoints remain incomplete. Campaign Blob uploads, budgets, and targeting are not implemented. Do not treat this partial API as production-ready admin functionality.

## Local backend development

Follow the [root README local installation](../README.md#local-installation) to configure SQL Server/Redis, apply migrations, and run the API. Use a development token with the appropriate role claim to call the protected admin endpoints. Run the authorization matrix with:

```bash
DOTNET_ROLL_FORWARD=Major dotnet test backend/Remito.UnitTests \
  --filter FullyQualifiedName~AdminRbacMatrixTests
```

## Production

Production installation and deployment are documented in the [root README](../README.md#production-installation) and [infrastructure guide](../docs/infra.md). The portal itself cannot currently be deployed; complete and security-review the UI, staff authentication, CSRF/antiforgery, workflows, and remaining audit coverage before enabling admin operations in production.
