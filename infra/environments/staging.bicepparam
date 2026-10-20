using '../main.bicep'

param environment = 'staging'
param location = 'eastus'
param resourceGroupName = 'remito-staging-rg'
param appName = 'remito-api-staging-001'
param sqlServerName = 'remito-staging-sql-001'
param sqlDatabaseName = 'remito'
param keyVaultName = 'remito-staging-kv-001'
param storageAccountName = 'remitostagingassets001'
param redisName = 'remito-staging-redis-001'
param appServicePlanSku = 'P1v3'
param appServicePlanTier = 'PremiumV3'
param redisSkuName = 'Standard'
param redisSkuFamily = 'C'
param redisSkuCapacity = 2
param sqlDatabaseSku = 'S2'
param sqlDatabaseTier = 'Standard'
param sqlBackupRetentionDays = 14
