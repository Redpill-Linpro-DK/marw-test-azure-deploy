param tags object
param location string
param queueNames array
param serviceBusNamespaceName string
param topicNames array

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-01-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  sku: {
    name: 'Standard' // Standard Tier
  }
  properties: {
  }
  tags: tags
  identity: {
    type: 'SystemAssigned' // Enabling System Assigned Managed Identity
  }
}

resource serviceBusQueues 'Microsoft.ServiceBus/namespaces/queues@2022-01-01-preview' = [for queueName in queueNames: {
  parent: serviceBusNamespace
  name: queueName
  properties: {
    // Define properties for the queue as needed
  }
}]

resource topicsAndSubscriptions 'Microsoft.ServiceBus/namespaces/topics@2022-01-01-preview' = [for topicName in topicNames: {
  name: topicName
  parent: serviceBusNamespace
  properties: {}
}]

resource queueLocks 'Microsoft.Authorization/locks@2020-05-01' = [
  for (queueName, i) in queueNames: {
    name: '${queueName}-lock'
    properties: {
      level: 'CanNotDelete' // Options: 'CanNotDelete' or 'ReadOnly'
      notes: 'Lock to protect the Queue resource from accidental deletion'
    }
    scope: serviceBusQueues[i]
  }
]

resource topicLocks 'Microsoft.Authorization/locks@2020-05-01' = [
  for (topicName, i) in topicNames: {
    name: '${topicName}-lock'
    properties: {
      level: 'CanNotDelete' // Options: 'CanNotDelete' or 'ReadOnly'
      notes: 'Lock to protect the Topic resource from accidental deletion'
    }
    scope: topicsAndSubscriptions[i]
  }
]

output serviceBusNamespaceId string = serviceBusNamespace.id
output serviceBusNamespaceIdentityPrincipalId string = serviceBusNamespace.identity.principalId
output serviceBusNamespaceIdentityTenantId string = serviceBusNamespace.identity.tenantId
