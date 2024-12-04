@description('The name of the storage account.')
param storageAccountName string = 'marwgithubspikedev'

@description('The location where the storage account will be created.')
param location string = resourceGroup().location

@description('The SKU (pricing tier) for the storage account.')
param storageAccountSku string = 'Standard_LRS'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: storageAccountSku
  }
  kind: 'StorageV2'
}

output storageAccountId string = storageAccount.id
