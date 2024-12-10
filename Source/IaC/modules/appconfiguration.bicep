param tags object

param appConfigName string
param disableLocalAuth bool
param enablePurgeProtection bool
param location string
param sku string
param softDeleteRetentionInDays int

resource appconfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: appConfigName
  location: location
  tags: tags
  sku: {
    name: sku
  }
  identity:{
    type: 'SystemAssigned'
  }
  properties: {
    softDeleteRetentionInDays: softDeleteRetentionInDays
    enablePurgeProtection: enablePurgeProtection
    disableLocalAuth: disableLocalAuth
  }
}

output appconfig object = appconfig
