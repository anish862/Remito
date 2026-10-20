# Infrastructure and delivery

Remito uses GitHub Actions and Azure Bicep. The environment parameter files are deployment inputs, not credentials. The example globally scoped names end in `001`; replace them with names available in the target Azure tenant before the first deployment.

## Environments and resources

Deploy each environment at subscription scope with its matching Bicep parameter file. Every resource is tagged with `application=Remito`, the environment, and `managedBy=Bicep`.

| Environment | Parameter file | App Service plan | Azure SQL | Redis | Backup retention |
|---|---|---|---|---|---:|
| dev | `infra/environments/dev.bicepparam` | Standard S1 (supports deployment slots) | Standard S0 | Standard C1 | 7 days |
| staging | `infra/environments/staging.bicepparam` | PremiumV3 P1v3 | Standard S2 | Standard C2 | 14 days |
| prod | `infra/environments/prod.bicepparam` | PremiumV3 P1v3 | General Purpose GP_Gen5_2 | Premium P1 | 35 days |

The following inventory applies to each environment. Owners are accountable for operations, access reviews, and alerts.

| Resource(s) | Purpose / security configuration | Owner |
|---|---|---|
| Resource group | Isolated lifecycle and RBAC boundary for the environment | Platform / Cloud (`@anish862`) |
| Virtual network; App Service integration and private-endpoint subnets | App outbound integration and private-link network boundary | Platform / Cloud (`@anish862`) |
| Four private DNS zones and VNet links (`privatelink.database.windows.net`, `privatelink.redis.cache.windows.net`, `privatelink.blob.core.windows.net`, `privatelink.vaultcore.azure.net`) | Private name resolution for data services | Platform / Cloud (`@anish862`) |
| App Service plan, Linux API app, `staging` deployment slot | .NET 8 API, system-assigned managed identities, HTTPS-only, TLS 1.2+, `/health` health check, VNet integration | Backend (`@anish862`) |
| App Service app settings and slot configuration | Environment-specific configuration; secrets use Key Vault references; configuration settings remain sticky during slot swap | Backend (`@anish862`) |
| Azure SQL logical server and database | Public network disabled, private endpoint, Microsoft Entra-only authentication when an admin is configured | Data / Backend (`@anish862`) |
| SQL short-term backup retention policy | Azure SQL automatic point-in-time backups with environment-specific retention and geo-redundant backup storage | Data / Backend (`@anish862`) |
| Azure Cache for Redis | TLS-only, public network disabled, private endpoint | Backend (`@anish862`) |
| Key Vault | RBAC authorization, purge protection, soft delete, public network disabled and private endpoint | Platform / Security (`@anish862`) |
| Key Vault Secrets User role assignments (API and slot identities) | Allows App Service to resolve Key Vault references without repository credentials | Platform / Security (`@anish862`) |
| Storage account, Blob service, `logos` and `creatives` private containers | Private logo and creative storage; public blob access and shared-key access disabled; soft delete enabled | Backend (`@anish862`) |
| Storage Blob Data Contributor role assignments (API and slot identities) | Managed-identity access to private Blob containers | Platform / Backend (`@anish862`) |
| Log Analytics workspace and Application Insights component | Centralized API logs, traces, and request telemetry | Platform / Observability (`@anish862`) |
| Azure Monitor action group | Optional email notification target; configure `ALERT_EMAIL` to enable delivery | Platform / On-call (`@anish862`) |
| Three scheduled-query alert rules | API 5xx rate >5%, request P95 >400 ms, and `rate_freshness_seconds` >900 seconds | Backend / On-call (`@anish862`) |
| SQL, Redis, Blob, and Key Vault private endpoints with their private DNS zone groups | Private access paths from the environment VNet and automatic private name registration | Platform / Cloud (`@anish862`) |

The rate-freshness alert expects the API to emit an Application Insights custom metric named `rate_freshness_seconds`, with values in seconds. It will not report rate freshness until that instrumentation is implemented and sending telemetry.

The current API health check is `/health` and does not require SQL or Redis. SQL access by the app and migration runner requires a Microsoft Entra SQL administrator and database principals/permissions to be provisioned for the corresponding identities. The EF migration job authenticates with the Azure CLI identity; grant it the required database DDL permissions before enabling migrations. The application identity should receive only the database roles it needs.

## Initial Azure provisioning

1. Create an Azure OIDC application/service principal for GitHub Actions. Grant it subscription-scope permissions to create resource groups and deploy the listed resources, plus the necessary role-assignment permission. Configure federated credentials for `repo:anish862/Remito:pull_request` and the deployment environment subjects `repo:anish862/Remito:environment:dev`, `repo:anish862/Remito:environment:staging`, `repo:anish862/Remito:environment:production`, and `repo:anish862/Remito:environment:database-migrations`; do not create a client secret.
2. Choose a Microsoft Entra SQL administrator (normally an administrator group). Set its object ID, display/login name, and principal type in the GitHub repository variables below. This identity is configured as the SQL administrator and SQL authentication is disabled when the ID is provided.
3. Set the `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` repository secrets and the SQL administrator repository variables. Set up a GitHub environment for each deployment environment and the variables/secrets listed below.
4. From an Azure CLI session with subscription deployment permissions, deploy each environment. For example:

   ```bash
   az account set --subscription "$AZURE_SUBSCRIPTION_ID"
   az deployment sub create \
     --name remito-dev \
     --location eastus \
     --template-file infra/main.bicep \
     --parameters infra/environments/dev.bicepparam \
       sqlAadAdminObjectId="$AZURE_SQL_AAD_ADMIN_OBJECT_ID" \
       sqlAadAdminLogin="$AZURE_SQL_AAD_ADMIN_LOGIN" \
       alertEmail="${ALERT_EMAIL:-}"
   ```

   Repeat with the staging and prod parameter files. Choose names that are globally available; storage account names must be 3-24 lowercase alphanumeric characters. The current workflow runs a dev `what-if` for PRs; it does not apply infrastructure automatically.
5. After deployment, add required Key Vault secrets using an approved secure process from a network that can reach the private endpoint. Do not put secret values in `.bicepparam`, Bicep outputs, workflow files, app settings, or PR comments. The app references these versionless Key Vault secret URIs through its system-assigned managed identity:
   - `redis-connection-string`
   - `jwt-signing-key`
   - `rate-provider-api-key`
   - `firebase-service-account`
   - `partner-postback-secret`
6. Configure `Firebase__ProjectId` for each environment and set `Notifications__Enabled` to `true` only after FCM credentials are provisioned and tested. Notifications are disabled by default. Redis initialization is explicitly disabled (`Redis__Enabled=false`) until its Key Vault connection string is provisioned; change that Bicep setting and redeploy only after the private connection is verified. This keeps `/health` available while optional backing services are being configured. The Key Vault reference settings are intentionally placeholders until their corresponding secrets have been provisioned.

### GitHub configuration

Repository secrets (OIDC identifiers; no client secret):

| Name | Value |
|---|---|
| `AZURE_CLIENT_ID` | Azure application/client ID |
| `AZURE_TENANT_ID` | Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Target subscription ID |
| `ANDROID_KEYSTORE_BASE64` | Base64-encoded Android release keystore |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_ALIAS` | Signing key alias |
| `ANDROID_KEY_PASSWORD` | Signing key password |

Repository variables used by infrastructure validation:

| Name | Value |
|---|---|
| `AZURE_SQL_AAD_ADMIN_OBJECT_ID` | Entra object ID of the SQL administrator |
| `AZURE_SQL_AAD_ADMIN_LOGIN` | Entra SQL administrator name |
| `ALERT_EMAIL` | Optional operations email address |
| `SQL_SERVER_NAME_DEV`, `SQL_SERVER_NAME_STAGING`, `SQL_SERVER_NAME_PROD` | SQL logical server names used by the gated migration job |
| `SQL_DATABASE_NAME_DEV`, `SQL_DATABASE_NAME_STAGING`, `SQL_DATABASE_NAME_PROD` | Database names used by the gated migration job |

Create GitHub environments `dev`, `staging`, `production`, and `database-migrations`. Set `APP_NAME` and `AZURE_RESOURCE_GROUP` as environment variables in `dev`, `staging`, and `production`; each must match that environment's Bicep parameters. Add required reviewers to `production` and `database-migrations`; add the intended deployment branch/tag restrictions to all environments. The environment secrets can inherit the repository OIDC secrets. The migration job selects its SQL server and database from repository variables matching the selected workflow-dispatch environment.

Tagging a release `v*` builds one backend package, deploys it to staging and waits for the `/health` check before requesting production approval. Each deployment first updates its `staging` slot, validates `https://<app>-staging.azurewebsites.net/health`, swaps the healthy slot into production, then retries production `/health`. A failed candidate health check prevents the swap; if production remains unhealthy after the swap, the workflow automatically swaps the prior version back and verifies its health. Deployments are serialized per environment. The production GitHub environment approval is the manual gate.

`workflow_dispatch` in the backend workflow can select an environment and optionally request EF migrations. The migrations job requires approval in `database-migrations`; it skips cleanly when no EF migration files exist. Do not request the migration option until the SQL identity, permissions, and migration set are ready.

## Pull request and branch protection

Enable branch protection (or a ruleset) for `main` in GitHub:

- Require a pull request before merging, at least one approving review, and a CODEOWNERS review (`.github/CODEOWNERS`).
- Dismiss stale approvals and require approval of the most recent reviewable push.
- Require conversation resolution and the successful status checks `backend-build-test`, `backend-dependency-review`, `android-quality`, and `infrastructure-build`.
- Require branches to be up to date before merging; block force pushes and branch deletion. Apply the rules to administrators as well.
- Require signed commits if that is part of the organization's standard.

The backend, Android, and infrastructure workflows run on every PR. The infrastructure workflow builds all Bicep modules and, for same-repository PRs with Azure OIDC configured, runs a dev subscription `what-if`. It uploads the full JSON result as the `bicep-what-if-dev` artifact and posts a preview/link on the PR. Fork PRs still build Bicep but cannot access Azure credentials, so they cannot run the cloud `what-if`.

## Rollback and release validation

Slot swaps keep the prior production version in the `staging` slot. To roll back, run **Actions → Roll back API deployment**, select the target environment, and approve its environment protection if configured. The workflow swaps the previous slot back into production and verifies `/health`. For the QA gate, perform this rollback in staging before production promotion and confirm the previous release serves healthy traffic; redeploy the approved release afterward through the normal staging/production workflow. A rollback deliberately leaves the previous release live. If the deployment workflow already performed its automatic rollback after a failed production health check, do not run the manual rollback workflow: the rejected candidate remains in the staging slot until the next deployment replaces it.

The Android tag pipeline signs and uploads an AAB artifact; Play Store publishing is not configured. The signed keystore is available only as Actions secrets and is never checked into the repository.

## Local validation

```bash
az bicep build --file infra/main.bicep
az deployment sub what-if \
  --location eastus \
  --template-file infra/main.bicep \
  --parameters infra/environments/dev.bicepparam \
    sqlAadAdminObjectId="$AZURE_SQL_AAD_ADMIN_OBJECT_ID" \
    sqlAadAdminLogin="$AZURE_SQL_AAD_ADMIN_LOGIN" \
```
