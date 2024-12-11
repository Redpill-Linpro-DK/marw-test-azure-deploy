param developerAccessAadGroupId string
param applicationName string
param env string
param postfixCount string
param uniqueDeployId string

// Variables
var shouldAssignDeveloperWriteAccess = (env == 'dev' || env == 'uat' || env == 'test') && developerAccessAadGroupId != ''
var shouldAssignDeveloperReadAccess = developerAccessAadGroupId != ''

// Truncate application name to 7 characters to ensure KeyVault name does not exceed 24 characters.
var truncatedApplicationName = length(applicationName) > 7 ? take(applicationName, 7) : applicationName

var cosmosDbName = toLower('cosmos-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var serviceBusNamespaceName = toLower('sbns-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var appConfigurationName = toLower('config-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var storageAccountName = toLower('st${applicationName}${env}${uniqueDeployId}${postfixCount}')
var keyVaultName = toLower('kv-${truncatedApplicationName}-${env}-${uniqueDeployId}-${postfixCount}')

// App Configuration (reader)
var appConfigurationDataOwner = '5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b'
resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' existing = {
  name: appConfigurationName
}
resource appConfigWriterRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperWriteAccess) {
  name: guid(appConfig.id, developerAccessAadGroupId, 'App Configuration Data Owner')
  scope: appConfig
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', appConfigurationDataOwner)
    principalType: 'Group'
  }
}
var appConfigurationDataReader = '516239f1-63e1-4d78-a4de-a74fb236a071'
resource appConfigReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperReadAccess) {
  name: guid(appConfig.id, developerAccessAadGroupId, 'App Configuration Data Reader')
  scope: appConfig
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', appConfigurationDataReader)
    principalType: 'Group'
  }
}

// Service Bus (data owner)
var azureServiceBusDataOwnerRole = '090c5cfd-751d-490a-894a-3ce6f1109419'
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' existing = {
  name: serviceBusNamespaceName
}
resource namespaceContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (shouldAssignDeveloperWriteAccess) {
  name: guid(serviceBusNamespace.id, developerAccessAadGroupId, 'Azure Service Bus Data Owner')
  scope: serviceBusNamespace
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleAssignments@2022-04-01', azureServiceBusDataOwnerRole)
    principalType: 'Group'
  }
}

// Storage Account (blobs, tables)
var storageBlobDataContributorRole = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var tableStorageContributorRole = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-02-01' existing = {
  name: storageAccountName
}
resource blobContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperWriteAccess) {
  name: guid(storageAccount.id, developerAccessAadGroupId, 'Storage Blob Data Contributor')
  scope: storageAccount
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRole)
    principalType: 'Group'
  }
}
resource tableContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperWriteAccess) {
  name: guid(storageAccount.id, developerAccessAadGroupId, 'Table Storage Data Contributor')
  scope: storageAccount
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableStorageContributorRole)
    principalType: 'Group'
  }
}
var storageBlobDataReaderRole = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
var tableStorageReaderRole = '76199698-9eea-4c19-bc75-cec21354c6b6'
resource blobReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperReadAccess) {
  name: guid(storageAccount.id, developerAccessAadGroupId, 'Storage Blob Data Reader')
  scope: storageAccount
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataReaderRole)
    principalType: 'Group'
  }
}
resource tableReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperReadAccess) {
  name: guid(storageAccount.id, developerAccessAadGroupId, 'Table Storage Data Reader')
  scope: storageAccount
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableStorageReaderRole)
    principalType: 'Group'
  }
}

// Cosmos DB (read and write data)
var readerWriterRoleDefinitionId = '/${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.DocumentDB/databaseAccounts/${databaseAccount.name}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
resource databaseAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = {
  name: cosmosDbName
}
resource sqlWriterRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-04-15' = if (shouldAssignDeveloperWriteAccess) {
  name: guid(readerWriterRoleDefinitionId, developerAccessAadGroupId, databaseAccount.id, 'Reader/Writer Role')
  parent: databaseAccount
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: readerWriterRoleDefinitionId
    scope: databaseAccount.id
  }
}
var readerRoleDefinitionId = '/${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.DocumentDB/databaseAccounts/${databaseAccount.name}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000001'
resource sqlReaderRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-04-15' = if (shouldAssignDeveloperReadAccess) {
  name: guid(readerRoleDefinitionId, developerAccessAadGroupId, databaseAccount.id, 'Reader Role')
  parent: databaseAccount
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: readerRoleDefinitionId
    scope: databaseAccount.id
  }
}

// KeyVault
var keyVaultSecretsOfficerRole = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
var keyVaultCryptoOfficerRole = '14b46e9e-c2b7-41b4-b07b-48a6ebf60603'
var keyVaultCertificateOfficerRole = 'a4417e6f-fecd-4de8-b567-7b0420556985'

var keyVaultSecretsUserRole = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultCryptoUserRole = '12338af0-0e69-4776-bea7-57ae8d297424'
var keyVaultCertificateUserRole = 'db79e9a7-68ee-4b58-9aeb-b90e7c24fcba'

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: keyVaultName
}

resource keyVaultSecretOfficerRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if(shouldAssignDeveloperWriteAccess) {
  name: guid(keyVault.id, developerAccessAadGroupId, keyVaultSecretsOfficerRole)
  scope: keyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficerRole)
    principalType: 'Group'
  }
}

resource keyVaultSecretUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if(shouldAssignDeveloperReadAccess) {
  name: guid(keyVault.id, developerAccessAadGroupId, keyVaultSecretsUserRole)
  scope: keyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRole)
    principalType: 'Group'  
  }
}

resource keyVaultCryptoOfficerRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if(shouldAssignDeveloperWriteAccess) {
  name: guid(keyVault.id, developerAccessAadGroupId, keyVaultCryptoOfficerRole)
  scope: keyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoOfficerRole)
    principalType: 'Group'  
  }
}

resource keyVaultCryptoUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperReadAccess) {
  name: guid(keyVault.id, developerAccessAadGroupId, keyVaultCryptoUserRole)
  scope: keyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoUserRole)
    principalType: 'Group'  
  }
}

resource keyVaultCertificateOfficerRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if(shouldAssignDeveloperWriteAccess) {
  name: guid(keyVault.id, developerAccessAadGroupId, keyVaultCertificateOfficerRole)
  scope: keyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCertificateOfficerRole)
    principalType: 'Group'  
  }
}

resource keyVaultCertificateUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperReadAccess) {
  name: guid(keyVault.id, developerAccessAadGroupId, keyVaultCertificateUserRole)
  scope: keyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCertificateUserRole)
    principalType: 'Group'  
  }
}
