param storageAccountName string
param containerNames array
param tableNames array
param location string
param tags object

// Storage account
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    supportsHttpsTrafficOnly: true
  }
  tags: tags
}

// Containers
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  name: 'default'
  parent: storageAccount
}
resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2019-06-01' = [for (name, i) in containerNames: {
  name: containerNames[i]
  parent: blobService
  properties: {
    publicAccess: 'None'
  }
}]

// Tables
resource tableServices 'Microsoft.Storage/storageAccounts/tableServices@2023-01-01' = {
  name: 'default'
  parent: storageAccount
}
resource tables 'Microsoft.Storage/storageAccounts/tableServices/tables@2019-06-01' = [for (name, i) in tableNames: {
  name: tableNames[i]
  parent: tableServices
}]
