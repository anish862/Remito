using '../main.bicep'

param environment = 'dev'
param location = 'eastus'
param resourceGroupName = 'remito-dev-rg'
param appName = 'remito-api-dev-001'
param sqlServerName = 'remito-dev-sql-001'
param sqlDatabaseName = 'remito'
param keyVaultName = 'remito-dev-kv-001'
param storageAccountName = 'remitodevassets001'
param redisName = 'remito-dev-redis-001'
param appServicePlanSku = 'S1'
param appServicePlanTier = 'Standard'
param redisSkuName = 'Standard'
param redisSkuFamily = 'C'
param redisSkuCapacity = 1
param sqlDatabaseSku = 'S0'
param sqlDatabaseTier = 'Standard'
param sqlBackupRetentionDays = 7
