param addressSpace string
param applicationName string
param env string
param internalNetworkName string
param location string
param postfixCount string
param subnetName string = toLower('snet-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}')
param subnetPrefix string
param tags object
param uniqueDeployId string
param vnetName string = toLower('vnet-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}')

// Environment-specific address spaces if needed
var addressSpaces = {
  dev: '10.0.0.0/16'
  test: '10.1.0.0/16'
  prod: '10.2.0.0/16'
}

// Use a specific address space for each environment
var defaultAddressSpace = '10.0.0.0/16'
var selectedAddressSpace = (addressSpace != '' ) ? addressSpace : (env == 'dev') ? addressSpaces.dev : (env == 'test') ? addressSpaces.test : (env == 'prod') ? addressSpaces.prod : defaultAddressSpace

// Default subnet prefix within the respective address space
var subnetPrefixes = {
  dev: '10.0.0.0/24'
  test: '10.1.0.0/24'
  prod: '10.2.0.0/24'
}

// Select subnet prefix based on the environment
var defaultSubnetPrefix = '10.0.0.0/24'
var selectedSubnetPrefix = (subnetPrefix != '') ? subnetPrefix : (env == 'dev') ? subnetPrefixes.dev : (env == 'test') ? subnetPrefixes.test : (env == 'prod') ? subnetPrefixes.prod : defaultSubnetPrefix

resource vnet 'Microsoft.Network/virtualNetworks@2021-02-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        selectedAddressSpace
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: selectedSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
          delegations: [
            {
              name: 'Microsoft.Web/serverFarms'
              properties: {
                serviceName: 'Microsoft.Web/serverfarms'
              }
            }
          ]
        }
      }
    ]
  }
}

output vnetId string = vnet.id
