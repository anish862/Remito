param environment string
param location string
param tags object

var vnetName = 'remito-${environment}-vnet'
var sqlPrivateDnsZoneName = 'privatelink${az.environment().suffixes.sqlServerHostname}'
var storagePrivateDnsZoneName = 'privatelink.blob.${az.environment().suffixes.storage}'
var privateDnsZones = [
  sqlPrivateDnsZoneName
  'privatelink.redis.cache.windows.net'
  storagePrivateDnsZoneName
  'privatelink.vaultcore.azure.net'
]

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-09-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.20.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'appservice-integration'
        properties: {
          addressPrefix: '10.20.0.0/24'
          delegations: [
            {
              name: 'appServiceDelegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
          privateEndpointNetworkPolicies: 'Enabled'
        }
      }
      {
        name: 'private-endpoints'
        properties: {
          addressPrefix: '10.20.1.0/24'
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

resource privateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = [for zoneName in privateDnsZones: {
  name: zoneName
  location: 'global'
  tags: tags
}]

resource privateDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = [for (zoneName, i) in privateDnsZones: {
  parent: privateDnsZone[i]
  name: 'link-${environment}'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: virtualNetwork.id
    }
  }
}]

output virtualNetworkId string = virtualNetwork.id
output appServiceIntegrationSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnetName, 'appservice-integration')
output privateEndpointSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnetName, 'private-endpoints')
output sqlPrivateDnsZoneId string = privateDnsZone[0].id
output privateDnsZoneIds object = {
  redis: privateDnsZone[1].id
  storage: privateDnsZone[2].id
  keyVault: privateDnsZone[3].id
}
