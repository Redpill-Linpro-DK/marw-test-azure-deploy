param applicationName string
param env string
param uniqueDeployId string
param postfixCount string
param topicName string
param topicSubscriptions array

var serviceBusName = toLower('sbns-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-01-01-preview' existing = {
  name: serviceBusName
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2022-01-01-preview' existing = {
  name: topicName
  parent: serviceBusNamespace
}

resource subscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-01-01-preview' = [
  for (subscription, i) in topicSubscriptions: {
    name: subscription.subscriptionName
    parent: topic
    properties: {}
  }
]

resource serviceBusRule 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2021-06-01-preview' = [
  for (subscription, i) in topicSubscriptions: if (length(subscription.labelFilter) > 0) {
    name: '${subscriptions[i].name}Rule'
    parent: subscriptions[i]
    properties: {
      filterType: 'SqlFilter'
      sqlFilter: {
        sqlExpression: 'sys.label = \'${join(subscription.labelFilter, '\' OR sys.label = \'')}\''
        compatibilityLevel: 20
      }
    }
  }
]

resource subscriptionLocks 'Microsoft.Authorization/locks@2020-05-01' = [
  for (subscription, i) in topicSubscriptions: {
    name: '${subscription.subscriptionName}-lock'
    properties: {
      level: 'CanNotDelete' // Options: 'CanNotDelete' or 'ReadOnly'
      notes: 'Lock to protect the Subscription resource from accidental deletion'
    }
    scope: subscriptions[i]
  }
]
