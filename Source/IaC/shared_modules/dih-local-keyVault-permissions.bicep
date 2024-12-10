param functionManagedIdentity string
param env string
param developerAccessAadGroupId string
param localKeyVaultName string

var shouldAssignDeveloperWriteAccess = (env == 'dev' || env == 'uat' || env == 'test') && developerAccessAadGroupId != ''
var shouldAssignDeveloperReadAccess = developerAccessAadGroupId != ''

resource localKeyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' existing = {
  name: localKeyVaultName
}

var keyVaultSecretsUserRole = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultCryptoUserRole = '12338af0-0e69-4776-bea7-57ae8d297424'
var keyVaultCertificateUserRole = 'db79e9a7-68ee-4b58-9aeb-b90e7c24fcba'

var keyVaultSecretsOfficerRole = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
var keyVaultCryptoOfficerRole = '14b46e9e-c2b7-41b4-b07b-48a6ebf60603'
var keyVaultCertificateOfficerRole = 'a4417e6f-fecd-4de8-b567-7b0420556985'

resource groupKeyVaultSecretOfficerRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperWriteAccess) {
  name: guid(localKeyVault.id, developerAccessAadGroupId, keyVaultSecretsOfficerRole)
  scope: localKeyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficerRole)
    principalType: 'Group'
  }
}

resource groupKeyVaultSecretUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperReadAccess) {
  name: guid(localKeyVault.id, developerAccessAadGroupId, keyVaultSecretsUserRole)
  scope: localKeyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRole)
    principalType: 'Group'
  }
}

resource groupKeyVaultCryptoOfficerRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperWriteAccess) {
  name: guid(localKeyVault.id, developerAccessAadGroupId, keyVaultCryptoOfficerRole)
  scope: localKeyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoOfficerRole)
    principalType: 'Group'
  }
}

resource groupKeyVaultCryptoUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperReadAccess) {
  name: guid(localKeyVault.id, developerAccessAadGroupId, keyVaultCryptoUserRole)
  scope: localKeyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoUserRole)
    principalType: 'Group'
  }
}

resource groupKeyVaultCertificateOfficerRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperWriteAccess) {
  name: guid(localKeyVault.id, developerAccessAadGroupId, keyVaultCertificateOfficerRole)
  scope: localKeyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCertificateOfficerRole)
    principalType: 'Group'
  }
}

resource groupKeyVaultCertificateUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (shouldAssignDeveloperReadAccess) {
  name: guid(localKeyVault.id, developerAccessAadGroupId, keyVaultCertificateUserRole)
  scope: localKeyVault
  properties: {
    principalId: developerAccessAadGroupId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCertificateUserRole)
    principalType: 'Group'
  }
}

resource funcKeyVaultSecretUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(localKeyVault.id, functionManagedIdentity, keyVaultSecretsUserRole)
  scope: localKeyVault
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRole)
    principalType: 'ServicePrincipal'
  }
}

resource funcKeyVaultCryptoUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(localKeyVault.id, functionManagedIdentity, keyVaultCryptoUserRole)
  scope: localKeyVault
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoUserRole)
    principalType: 'ServicePrincipal'
  }
}

resource funcKeyVaultCertificateUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(localKeyVault.id, functionManagedIdentity, keyVaultCertificateUserRole)
  scope: localKeyVault
  properties: {
    principalId: functionManagedIdentity
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCertificateUserRole)
    principalType: 'ServicePrincipal'
  }
}
