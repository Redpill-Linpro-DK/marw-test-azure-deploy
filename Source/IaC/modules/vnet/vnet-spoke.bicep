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

// Parameters for NAT Gateway and Public IP
param publicIpName string = toLower('pub-ip-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}') 
param natGwName string = toLower('natgw-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}')
param skuName string = 'Standard'
param skuTier string = 'Regional'

// Parameters for Routing
param routeTableName string = 'rtb-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}'
param nextHopIpAddress string // This is the hub address in a peered network
param routeAddressPrefix string = '10.0.0.0/8' // TODO: Adjust this to the address space of the hub network

// Parameters for Network Security Groups (NSG)
param nsgName string = 'nsg-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}'

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


// Public IP for NAT Gateway
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

// NAT Gateway
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

// Route Table and Route
resource routeTable 'Microsoft.Network/routeTables@2024-01-01' = {
  name: routeTableName
  location: location
  properties: {
    disableBgpRoutePropagation: false
    routes: [
      {
        name: 'On-Prem'
        properties: {
          addressPrefix: routeAddressPrefix
          nextHopType: 'VirtualAppliance'
          nextHopIpAddress: nextHopIpAddress
          hasBgpOverride: false
        }
      }
    ]
  }
}

// Virtual Network
resource virtualNetwork 'Microsoft.Network/virtualNetworks@2024-01-01' = {
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
          networkSecurityGroup: {
            id: networkSecurityGroup.id
          }
          natGateway: {
            id: natGateway.id
          }
          routeTable: {
            id: routeTable.id
          }
          privateEndpointNetworkPolicies: 'Enabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
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

// NSG Rules
resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2024-01-01' = {
  name: nsgName
  location: location
  tags: tags
}

resource AllowOutToOnPrem 'Microsoft.Network/networkSecurityGroups/securityRules@2024-01-01' = {
  parent: networkSecurityGroup
  name: 'AllowOutToOnPrem'
  properties: {
    protocol: '*'
    sourcePortRange: '*'
    sourceAddressPrefix: '*'
    destinationAddressPrefix: routeAddressPrefix
    access: 'Allow'
    priority: 120
    direction: 'Outbound'
    destinationPortRanges: [
      '80'
      '443'
    ]
  }
}

output vnetId string = virtualNetwork.id
output natGatewayId string = natGateway.id
output publicIpAddress string = publicIP.properties.ipAddress
