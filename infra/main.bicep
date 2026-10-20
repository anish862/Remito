targetScope = 'subscription'

@description('Deployment environment.')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string

@description('Azure region for all resources.')
param location string

@description('Resource group name for this environment.')
param resourceGroupName string

@description('Globally unique App Service name.')
param appName string

@description('Globally unique Azure SQL logical server name.')
param sqlServerName string

@description('Database name.')
param sqlDatabaseName string = 'remito'

@description('Globally unique Key Vault name.')
param keyVaultName string

@description('Globally unique Storage account name (3-24 lowercase alphanumeric characters).')
param storageAccountName string

@description('Globally unique Azure Cache for Redis name.')
param redisName string

@description('App Service plan SKU, for example B1 or P1v3.')
param appServicePlanSku string

@description('App Service plan SKU tier, for example Basic or PremiumV3.')
param appServicePlanTier string

@description('Redis SKU name: Standard or Premium.')
@allowed([
  'Standard'
  'Premium'
])
param redisSkuName string

@description('Redis SKU family: C for Standard or P for Premium.')
@allowed([
  'C'
  'P'
])
param redisSkuFamily string

@description('Redis SKU capacity.')
param redisSkuCapacity int

@description('Azure SQL database SKU name.')
param sqlDatabaseSku string

@description('Azure SQL database SKU tier.')
param sqlDatabaseTier string

@description('SQL database point-in-time restore retention days.')
@minValue(7)
@maxValue(35)
param sqlBackupRetentionDays int

@description('Object ID of the Microsoft Entra administrator for SQL. Supply the deployment identity or an administrator group.')
param sqlAadAdminObjectId string = ''

@description('Login name for the Microsoft Entra SQL administrator.')
param sqlAadAdminLogin string = ''

@description('Optional email recipient for Azure Monitor alerts.')
param alertEmail string = ''

var commonTags = {
  application: 'Remito'
  environment: environment
  managedBy: 'Bicep'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: commonTags
}

module network 'modules/network.bicep' = {
  name: 'network-${environment}'
  scope: resourceGroup
  params: {
    environment: environment
    location: location
    tags: commonTags
  }
}

module observability 'modules/observability.bicep' = {
  name: 'observability-${environment}'
  scope: resourceGroup
  params: {
    environment: environment
    location: location
    tags: commonTags
  }
}

module secureData 'modules/secure-data.bicep' = {
  name: 'secure-data-${environment}'
  scope: resourceGroup
  params: {
    environment: environment
    location: location
    keyVaultName: keyVaultName
    storageAccountName: storageAccountName
    redisName: redisName
    redisSkuName: redisSkuName
    redisSkuFamily: redisSkuFamily
    redisSkuCapacity: redisSkuCapacity
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    privateDnsZoneIds: network.outputs.privateDnsZoneIds
    tags: commonTags
  }
}

module database 'modules/database.bicep' = {
  name: 'database-${environment}'
  scope: resourceGroup
  params: {
    location: location
    sqlServerName: sqlServerName
    sqlDatabaseName: sqlDatabaseName
    sqlDatabaseSku: sqlDatabaseSku
    sqlDatabaseTier: sqlDatabaseTier
    sqlBackupRetentionDays: sqlBackupRetentionDays
    sqlAadAdminObjectId: sqlAadAdminObjectId
    sqlAadAdminLogin: sqlAadAdminLogin
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    sqlPrivateDnsZoneId: network.outputs.sqlPrivateDnsZoneId
    tags: commonTags
  }
}

module api 'modules/api.bicep' = {
  name: 'api-${environment}'
  scope: resourceGroup
  params: {
    environment: environment
    location: location
    appName: appName
    appServicePlanSku: appServicePlanSku
    appServicePlanTier: appServicePlanTier
    integrationSubnetId: network.outputs.appServiceIntegrationSubnetId
    keyVaultName: keyVaultName
    storageAccountName: storageAccountName
    sqlServerName: sqlServerName
    sqlDatabaseName: sqlDatabaseName
    applicationInsightsConnectionString: observability.outputs.applicationInsightsConnectionString
    tags: commonTags
  }
}

module alerts 'modules/alerts.bicep' = {
  name: 'alerts-${environment}'
  scope: resourceGroup
  params: {
    environment: environment
    location: location
    applicationInsightsId: observability.outputs.applicationInsightsId
    alertEmail: alertEmail
    tags: commonTags
  }
}

output provisionedResourceGroup string = resourceGroup.name
output apiUrl string = 'https://${appName}.azurewebsites.net'
