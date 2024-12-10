param applicationName string
param env string
param internalNetworkName string
param location string
param postfixCount string
param tags object
param uniqueDeployId string
param addressSpace string = ''
param subnetPrefix string = ''

param natGwName string = toLower('natgw-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}')
param publicIpName string = toLower('pub-ip-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}')
param subnetName string = toLower('snet-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}')
param vnetName string = toLower('vnet-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}')



param skuName string = 'Standard'
param skuTier string = 'Regional'


resource publicIP 'Microsoft.Network/publicIPAddresses@2023-09-01' = {
  name: publicIpName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    publicIPAddressVersion: 'IPv4'
    publicIPAllocationMethod: 'Static'
  }
}

resource natGateway 'Microsoft.Network/natGateways@2023-04-01' = {
  name: natGwName
  location: location
  tags: tags
  sku: {
    name: skuName
  } 
  properties: {
    publicIpAddresses: [
      {
        id: publicIP.id
      }
    ]
    idleTimeoutInMinutes: 4
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2021-02-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressSpace
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: subnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
          natGateway: {
            id: natGateway.id
          }
          delegations: [
            {
              name: 'Microsoft.Web/serverFarms'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
    ]
  }
}

output vnetId string = vnet.id
output natGatewayId string = natGateway.id
output natGatewayIP string = publicIP.properties.ipAddress
