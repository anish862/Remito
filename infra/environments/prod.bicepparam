using '../main.bicep'

param environment = 'prod'
param location = 'eastus'
param resourceGroupName = 'remito-prod-rg'
param appName = 'remito-api-prod-001'
param sqlServerName = 'remito-prod-sql-001'
param sqlDatabaseName = 'remito'
param keyVaultName = 'remito-prod-kv-001'
param storageAccountName = 'remitoprodassets001'
param redisName = 'remito-prod-redis-001'
param appServicePlanSku = 'P1v3'
param appServicePlanTier = 'PremiumV3'
param redisSkuName = 'Premium'
param redisSkuFamily = 'P'
param redisSkuCapacity = 1
param sqlDatabaseSku = 'GP_Gen5_2'
param sqlDatabaseTier = 'GeneralPurpose'
param sqlBackupRetentionDays = 35
