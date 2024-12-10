param apimServiceName string
param commonResourceGroupName string
param functionAppName string

// Reference the APIM service from the specified resource group
resource apimService 'Microsoft.ApiManagement/service@2023-05-01-preview' existing = {
  name: apimServiceName
  scope: resourceGroup(commonResourceGroupName)
}

// Reference the Function App from its resource group
resource functionApp 'Microsoft.Web/sites@2022-03-01' existing = {
  name: functionAppName
}

// Assign "Function App Contributor" role to APIM managed identity at the Function App level
resource functionAppRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(apimServiceName, functionApp.id, 'FunctionAppContributor')
  scope: functionApp // The scope is now the Function App
  properties: {
    // Use the principalId from the APIM service's identity
    principalId: apimService.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'de139f84-1756-47ae-9be6-808fbbe84772') // Function App Contributor role definition ID
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    apimService
    functionApp
  ]
}
