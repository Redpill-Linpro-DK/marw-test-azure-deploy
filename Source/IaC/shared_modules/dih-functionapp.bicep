param applicationName string
param componentName string
param env string
param postfixCount string
param uniqueDeployId string
param functionName string = toLower('func-${applicationName}-${componentName}-${env}-${uniqueDeployId}-${postfixCount}')
param location string
param tags object
param customSettings array = []
param aadAppClientId string = '' // Azure AD Application (client) ID, optional
param aadOpenIdIssuerUrl string = '' // Azure AD OpenID Issuer URL, optional
param developerAccessAadGroupId string
param useLocalKeyVault bool = false
param useGlobalKeyVault bool = false
param internalNetworkName string = ''
param allowVnetUsage bool = false
param functionsWorkerRuntime string = 'java'

// Variables
var usesVnet = internalNetworkName != '' && allowVnetUsage
var commonResourceGroupName = toLower('rg-${applicationName}-c-${env}-${postfixCount}')
var appConfigurationName = toLower('config-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var appInsightsName = toLower('appi-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var functionAppLocalStorageAccountName = toLower(take('stfunc${take(applicationName,6)}${take(env,4)}${pseudoHash}', 24)) // Ensure the storage account name is unique and adheres to naming limits
var pseudoHash = take(guid(functionName), 8)
var resolvedIssuerUrl = empty(aadOpenIdIssuerUrl) ? 'https://login.microsoftonline.com/${tenant().tenantId}/v2.0' : aadOpenIdIssuerUrl
var localKeyVaultName = toLower(take('kv-${take(applicationName,4)}-${take(componentName, 5)}-${take(env,4)}${pseudoHash}', 24))
var appServicePlanNameVNet = toLower('asp-${applicationName}-vnet-${env}-${uniqueDeployId}-${postfixCount}-plan')

var snetName = toLower('snet-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}')
var vnetName = toLower('vnet-${applicationName}-${internalNetworkName}-${env}-${uniqueDeployId}-${postfixCount}') 

// Existing resources used
resource appConfiguration 'Microsoft.AppConfiguration/configurationStores@2023-03-01' existing = {
  name: appConfigurationName
  scope: resourceGroup(commonResourceGroupName)
}
resource appInsights 'Microsoft.Insights/components@2020-02-02-preview' existing = {
  name: appInsightsName
  scope: resourceGroup(commonResourceGroupName)
}

resource appServicePlanVNet 'Microsoft.Web/serverfarms@2023-12-01' existing = if (usesVnet) {
  name: appServicePlanNameVNet
  scope: resourceGroup(commonResourceGroupName)
}

resource subnetWithStaticIpToOnPremSystems 'Microsoft.Network/virtualNetworks/subnets@2021-05-01' existing = if (usesVnet) {
  name: '${vnetName}/${snetName}'
  scope: resourceGroup(commonResourceGroupName)
}

// New storage account for Azure Functions runtime
resource functionAppStorageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: functionAppLocalStorageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    supportsHttpsTrafficOnly: true
  } 
  tags: tags
}


// Function app and consumption plan
resource functionApp 'Microsoft.Web/sites@2021-02-01' = {
  name: functionName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: (usesVnet == true ? appServicePlanVNet.id : consumptionPlan.id)
    virtualNetworkSubnetId: (usesVnet == true ? subnetWithStaticIpToOnPremSystems.id : null)
    clientAffinityEnabled: false
    httpsOnly: true
    siteConfig: {
      alwaysOn: (usesVnet == true ? true : false)
      vnetRouteAllEnabled: (usesVnet == true ? true : false)        
      appSettings: union(
        concat(
          [
            {
              name: 'FUNCTIONS_WORKER_RUNTIME'
              value: '${functionsWorkerRuntime}'
            }
            {
              name: 'FUNCTIONS_INPROC_NET8_ENABLED'
              value: '1'
            }
            {
              name: 'AzureWebJobsStorage'
              value: 'DefaultEndpointsProtocol=https;AccountName=${functionAppStorageAccount.name};AccountKey=${functionAppStorageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: 'InstrumentationKey=${appInsights.properties.InstrumentationKey}'
            }
            {
              name: 'AzureAppConfigurationEndpoint'
              value: 'https://${appConfiguration.name}.azconfig.io'
            }
            {
              name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
              value: 'DefaultEndpointsProtocol=https;AccountName=${functionAppStorageAccount.name};AccountKey=${functionAppStorageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
            }
            {
              name: 'WEBSITE_CONTENTSHARE'
              value: '${functionName}-${env}-${uniqueString(functionName)}'
            }
            {
              name: 'FUNCTIONS_EXTENSION_VERSION'
              value: '~4'
            }
          ],
          useLocalKeyVault ? [
            {
              name: 'LocalKeyVault:Uri'
              value: 'https://${localKeyVaultName}.vault.azure.net/'
            }
          ] : []
        ),  
        customSettings
      )
      netFrameworkVersion: 'v8.0'
      minTlsVersion: '1.2'
    }
  }
  tags: union(
    tags,
    usesVnet ? {
      VNet: 'true'
    } : {
      VNet: 'false'
    }
  )
  identity: {
    type: 'SystemAssigned'
  }
}

// Consumption  Plan
resource consumptionPlan 'Microsoft.Web/serverfarms@2023-12-01' = if(usesVnet == false){
  name: 'asp-${functionName}-plan'
  location: location
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false // For Windows
  }
  tags: tags
}

// Authentication Settings using authsettingsV2
resource functionAppAuthSettings 'Microsoft.Web/sites/config@2022-03-01' = if (!empty(aadAppClientId)) {
  name: '${functionName}/authsettingsV2'
  properties: {
    globalValidation: {
      requireAuthentication: true
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: aadAppClientId
          openIdIssuer: resolvedIssuerUrl
        }
      }
    }
    platform: {
      enabled: true
    }
    dependsOn: [
      functionApp
    ]
  }
}

module localKeyVault '../modules/keyvault.bicep' = if (useLocalKeyVault) {
  name: 'localKeyVault'
  params: {
    keyVaultName: localKeyVaultName
  }
}

module dihFunctionappPermissions './dih-functionapp-permissions.bicep' = {
  name: 'dihFunctionappPermissions'
  scope: resourceGroup(commonResourceGroupName)
  params: {
    applicationName: applicationName
    env: env
    postfixCount: postfixCount
    uniqueDeployId: uniqueDeployId
    functionManagedIdentity: functionApp.identity.principalId
    useGlobalKeyVault: useGlobalKeyVault
  }
}

module keyVaultPermissions './dih-local-keyVault-permissions.bicep' = if(useLocalKeyVault) {
  name: 'keyVaultPermissions'
  params: {
    developerAccessAadGroupId: developerAccessAadGroupId
    env: env
    functionManagedIdentity: functionApp.identity.principalId
    localKeyVaultName: localKeyVaultName
  }
  dependsOn: [
    localKeyVault
  ]
}

output functionAppName string = functionApp.name
