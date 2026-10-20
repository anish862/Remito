param environment string
param location string
param appName string
param appServicePlanSku string
param appServicePlanTier string
param integrationSubnetId string
param keyVaultName string
param storageAccountName string
param sqlServerName string
param sqlDatabaseName string
param applicationInsightsConnectionString string
param tags object

var appSettings = {
  ASPNETCORE_ENVIRONMENT: 'Production'
  Remito__Environment: environment
  ASPNETCORE_FORWARDEDHEADERS_ENABLED: 'true'
  APPLICATIONINSIGHTS_CONNECTION_STRING: applicationInsightsConnectionString
  ConnectionStrings__Default: 'Server=tcp:${sqlServerName}${az.environment().suffixes.sqlServerHostname},1433;Initial Catalog=${sqlDatabaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;'
  Redis__Enabled: 'false'
  Redis__ConnectionString: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}${az.environment().suffixes.keyvaultDns}/secrets/redis-connection-string/)'
  Jwt__SigningKey: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}${az.environment().suffixes.keyvaultDns}/secrets/jwt-signing-key/)'
  RateProviders__ApiKey: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}${az.environment().suffixes.keyvaultDns}/secrets/rate-provider-api-key/)'
  Firebase__ProjectId: 'configure-${environment}-firebase-project'
  Firebase__ServiceAccount: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}${az.environment().suffixes.keyvaultDns}/secrets/firebase-service-account/)'
  Notifications__Provider: 'fcm'
  Notifications__Enabled: 'false'
  PartnerPostback__Secret: '@Microsoft.KeyVault(SecretUri=https://${keyVaultName}${az.environment().suffixes.keyvaultDns}/secrets/partner-postback-secret/)'
  Storage__BlobServiceUri: 'https://${storageAccountName}.blob.${az.environment().suffixes.storage}'
  Storage__LogosContainer: 'logos'
  Storage__CreativesContainer: 'creatives'
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: appServicePlanSku
    tier: appServicePlanTier
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource api 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    virtualNetworkSubnetId: integrationSubnetId
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      http20Enabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/health'
      vnetRouteAllEnabled: true
    }
  }
}

resource apiSettings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: api
  name: 'appsettings'
  properties: appSettings
}

resource deploymentSlot 'Microsoft.Web/sites/slots@2023-12-01' = {
  parent: api
  name: 'staging'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    virtualNetworkSubnetId: integrationSubnetId
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      http20Enabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/health'
      vnetRouteAllEnabled: true
    }
  }
}

resource slotSettings 'Microsoft.Web/sites/slots/config@2023-12-01' = {
  parent: deploymentSlot
  name: 'appsettings'
  properties: appSettings
}

resource stickySettings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: api
  name: 'slotConfigNames'
  properties: {
    appSettingNames: [
      'ASPNETCORE_ENVIRONMENT'
      'Remito__Environment'
      'ConnectionStrings__Default'
      'Redis__Enabled'
      'Redis__ConnectionString'
      'Jwt__SigningKey'
      'RateProviders__ApiKey'
      'Firebase__ProjectId'
      'Firebase__ServiceAccount'
      'Notifications__Provider'
      'Notifications__Enabled'
      'PartnerPostback__Secret'
      'Storage__BlobServiceUri'
      'Storage__LogosContainer'
      'Storage__CreativesContainer'
    ]
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var blobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
resource apiKeyVaultRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appName, 'api', kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource slotKeyVaultRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appName, 'staging-slot', kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: deploymentSlot.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, appName, 'api', blobDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource slotStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, appName, 'staging-slot', blobDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: deploymentSlot.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output appName string = api.name
output appPrincipalId string = api.identity.principalId
output stagingSlotName string = deploymentSlot.name
