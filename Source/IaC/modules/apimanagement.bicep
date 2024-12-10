param apimServiceName string
param location string
param publisherName string 
param publisherEmail string 
param apimskuName string = 'Developer' // You can change this to Standard, Basic, or any other SKU as needed
param tags object
param cosmosDbName string = ''

resource apimService 'Microsoft.ApiManagement/service@2024-05-01' = {
  name: apimServiceName
  location: location
  sku: {
    name: apimskuName
    capacity: 1 // Default capacity, change as needed
  }
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publisherName: publisherName
    publisherEmail: publisherEmail    
    apiVersionConstraint: {
      minApiVersion: '2021-08-01'
    }
  }
}

// Conditional block for Cosmos DB and role assignment
var readerRoleDefinitionId = '/${subscription().id}/resourceGroups/${resourceGroup().name}/providers/Microsoft.DocumentDB/databaseAccounts/${databaseAccount.name}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000001'
resource databaseAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' existing = if (!empty(cosmosDbName)) {
  name: cosmosDbName
}
resource sqlRoleAssignment 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2023-04-15' = if (!empty(cosmosDbName)) {
  name: guid(readerRoleDefinitionId, apimService.id, databaseAccount.id, 'Reader Role')
  parent: databaseAccount
  properties: {
    principalId: apimService.identity.principalId
    roleDefinitionId: readerRoleDefinitionId
    scope: databaseAccount.id
  }
}

output apimServiceId string = apimService.id
output apimServicePrincipalId string = apimService.identity.principalId
