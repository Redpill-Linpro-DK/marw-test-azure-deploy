param functionManagedIdentity string
param applicationName string
param env string
param postfixCount string
param uniqueDeployId string
param useGlobalKeyVault bool

// Truncate application name to 7 characters to ensure KeyVault name does not exceed 24 characters.
var truncatedApplicationName = length(applicationName) > 7 ? take(applicationName, 7) : applicationName

// Variables
var cosmosDbName = toLower('cosmos-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var serviceBusNamespaceName = toLower('sbns-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var appConfigurationName = toLower('config-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var storageAccountName = toLower('st${applicationName}${env}${uniqueDeployId}${postfixCount}')
var appInsightsName = toLower('appi-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var globalKeyVaultName = toLower('kv-${truncatedApplicationName}-${env}-${uniqueDeployId}-${postfixCount}')

// App Configuration (reader)
var appConfigurationDataReader = '516239f1-63e1-4d78-a4de-a74fb236a071'
resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' existing = {
  name: appConfigurationName
}
resource appConfigRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(appConfig.id, functionManagedIdentity, 'App Configuration Data Reader')
  scope: appConfig
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', appConfigurationDataReader)
    principalType: 'ServicePrincipal'
  }
}

// Service Bus (data owner)
var azureServiceBusDataOwnerRole = '090c5cfd-751d-490a-894a-3ce6f1109419'
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' existing = {
  name: serviceBusNamespaceName
}
resource namespaceContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, functionManagedIdentity, 'Azure Service Bus Data Owner')
  scope: serviceBusNamespace
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleAssignments@2022-04-01', azureServiceBusDataOwnerRole)
    principalType: 'ServicePrincipal'
  }
}

// Storage Account (blobs, tables)
var storageBlobDataContributorRole = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var tableStorageContributorRole = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-02-01' existing = {
  name: length(storageAccountName) > 24 ? take(storageAccountName, 24) : storageAccountName
}
resource blobContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(storageAccount.id, functionManagedIdentity, 'Storage Blob Data Contributor')
  scope: storageAccount
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRole)
    principalType: 'ServicePrincipal'
  }
}
resource queueContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(storageAccount.id, functionManagedIdentity, 'Storage Queue Data Contributor')
  scope: storageAccount
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableStorageContributorRole)
    principalType: 'ServicePrincipal'
  }
}

// Cosmos DB (read and write data)
var readerWriterRoleDefinitionId = '/${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.DocumentDB/databaseAccounts/${databaseAccount.name}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
resource databaseAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: cosmosDbName
}
resource sqlRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-04-15' = {
  name: guid(readerWriterRoleDefinitionId, functionManagedIdentity, databaseAccount.id, 'Reader/Writer Role')
  parent: databaseAccount
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: readerWriterRoleDefinitionId
    scope: databaseAccount.id
  }
}

// App Insights (Monitoring Contributer)
var monitoringContributorRoleId = '749f88d5-cbae-40b8-bcfc-e573ddc772fa'
resource appInsights 'Microsoft.Insights/components@2020-02-02-preview' existing = {
  name: appInsightsName
}
resource monitoringContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(appInsights.id, functionManagedIdentity, monitoringContributorRoleId)
  scope: appInsights
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringContributorRoleId)
    principalType: 'ServicePrincipal'
  }
}

// KeyVault (Secrets Officer, Crypto Officer, Certificate Officer)
var keyVaultSecretsUserRole = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultCryptoUserRole = '12338af0-0e69-4776-bea7-57ae8d297424'
var keyVaultCertificateUserRole = 'db79e9a7-68ee-4b58-9aeb-b90e7c24fcba'

resource globalKeyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: globalKeyVaultName
}

resource globalKeyVaultSecretUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (useGlobalKeyVault) {
  name: guid(globalKeyVault.id, functionManagedIdentity, keyVaultSecretsUserRole)
  scope: globalKeyVault
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRole)
    principalType: 'ServicePrincipal'
  }
}

resource globalKeyVaultCryptoUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (useGlobalKeyVault) {
  name: guid(globalKeyVault.id, functionManagedIdentity, keyVaultCryptoUserRole)
  scope: globalKeyVault
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoUserRole)
    principalType: 'ServicePrincipal'
  }
}

resource globalKeyVaultCertificateUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (useGlobalKeyVault) {
  name: guid(globalKeyVault.id, functionManagedIdentity, keyVaultCertificateUserRole)
  scope: globalKeyVault
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCertificateUserRole)
    principalType: 'ServicePrincipal'
  }
}
