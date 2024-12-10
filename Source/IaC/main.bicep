param applicationName string
param location string
param env string
param postfixCount string
param tags object
param uniqueDeployId string
param domainObjectsJson string // JSON string parameter for domain objects
param publisherName string = 'tmp'
param publisherEmail string = 'tmp@tmp.com'
param configSentinelValue string = utcNow() // the default value is good as is - needs to be a param
// Default is empty to avoid the need to set in variable library
param internalNetworkName string = '' 

// Parse the JSON string to an object
var domainObjects = json(domainObjectsJson)

var ingestionDomainObjects = [
  for obj in domainObjects.Ingestion: {
    DataObjectTypeName: obj.DataObjectTypeName
    IdSubstitute: obj.IdSubstitute
    PartitionKey: obj.PartitionKey
    StoreInRaw: obj.StoreInRaw
  }
]
var dataRawStoredDataObjectTypeNames = map(
  filter(ingestionDomainObjects, obj => obj.StoreInRaw == true),
  obj => obj.DataObjectTypeName
)

// Truncate application name to 7 characters to ensure KeyVault name does not exceed 24 characters.
var truncatedApplicationName = length(applicationName) > 7 ? take(applicationName, 7) : applicationName

// Resource names
var apimName = toLower('apim-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var cosmosDbName = toLower('cosmos-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var serviceBusName = toLower('sbns-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var appConfigurationName = toLower('config-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var sharedStorageAccountName = toLower('st${applicationName}${env}${uniqueDeployId}${postfixCount}')
var applicationInsightsName = toLower('appi-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var messageMonitorWorkbookDisplayName = toLower('messagemonitor-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var keyVaultName = toLower('kv-${truncatedApplicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var appServicePlanName = toLower('asp-${applicationName}-vnet-${env}-${uniqueDeployId}-${postfixCount}-plan')

// APIM
module apimModule './modules/apimanagement.bicep' = {
  name: 'apimDeployment'
  params: {
    location: location
    apimServiceName: apimName
    tags: tags
    publisherName: publisherName
    publisherEmail: publisherEmail
    cosmosDbName: cosmosDbName
  }
  dependsOn: [cosmosDbModule]
}

// Application Configuration
resource appconfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: appConfigurationName
  location: location
  tags: tags
  sku: {
    name: 'standard'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    softDeleteRetentionInDays: 7
    enablePurgeProtection: false
    disableLocalAuth: false
  }
}

// AppInsights
module appInsightsModule './modules/appinsight.bicep' = {
  name: 'appInsight'
  params: {
    appInsightsName: applicationInsightsName
    location: location
    tags: tags
  }
}

//Message monitor
module messageMonitor './modules/messagemonitor.bicep' = {
  name: 'messageMonitor'
  params: {
    messageMonitorWorkbookDisplayName: messageMonitorWorkbookDisplayName
    appInsightsName: applicationInsightsName
    location: location
    tags: tags
  }
}

// CosmosDB
module cosmosDbModule './modules/cosmosdb.bicep' = {
  name: 'deployCosmosDb'
  params: {
    cosmosDbAccountName: cosmosDbName
    cosmosDbNamePrepared: 'prepared'
    cosmosDbNameRaw: 'raw'
    location: location
    tags: tags
    domainObjectsJson: domainObjectsJson
  }
}

// Key Vault
module keyVaultModule './modules/keyvault.bicep' = {
  name: 'keyVaultDeployment'
  params: {
    keyVaultName: keyVaultName
    location: location
    tags: tags
  }
}

// Vnet with NAT-gatway and static IP address configured for outbound traffic to Customer's On-Prem systems
// PS: This is only deployed if the "internalNetworkName" variable has been populated in the pipeline library. 
module vnetConnToOnPremThroughStaticIp './modules/vnet/vnet-public-ip.bicep' = if (internalNetworkName != '') {
  name: 'deployVnetWithNatgatewayAndPublicIpAddress'
  params: {
    applicationName: applicationName
    env: env
    postfixCount: postfixCount
    uniqueDeployId: uniqueDeployId
    location: location
    tags: tags
    internalNetworkName: internalNetworkName
  }
}

var skuName = 'S1'
var skuTier = 'Standard'

// Common App Service Plan for Function Apps that require VNET integration
resource appServicePlan 'Microsoft.Web/serverfarms@2021-02-01' = if(internalNetworkName != '') {
  name: appServicePlanName
  location: location
  kind: 'functionapp'
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    reserved: false // For Windows
  }
  tags: tags
}

// Servicebus, queues, topics, subscriptions for Data Raw
// Ingestion internal queues (usable when data is pushed to Ingestion and you offload processing before data raw to another Ingestion function, listening to these queues)
var dataRawQueueNames = [
  'dataraw-receive-fullbatch'
  'dataraw-receive-fullbatch-purge-plan'
  'dataraw-receive-fullbatch-purge-execute'
  'dataraw-receive-fullbatch-broadcast'
  'dataraw-receive-fullbatch-cleanup'
  'dataraw-receive-fullbatch-abort'
]
var ingestionChangeQueueNames = [for obj in ingestionDomainObjects: 'ingestion-${obj.DataObjectTypeName}-change']
var ingestionFullbatchQueueNames = [for obj in ingestionDomainObjects: 'ingestion-${obj.DataObjectTypeName}-fullbatch']
var serviceBusTopicNames = ['ingestion-change', 'ingestion-fullbatch', 'dataraw-change', 'dataprepared-change']

module serviceBusModule './modules/servicebus.bicep' = {
  name: 'queueDeployment'
  params: {
    location: location
    serviceBusNamespaceName: serviceBusName
    queueNames: concat(dataRawQueueNames, ingestionChangeQueueNames, ingestionFullbatchQueueNames)
    topicNames: serviceBusTopicNames
    tags: tags
  }
}

module ingestionChangeDataRawSubscrptionModule './shared_modules/dih-servicebus-topic-subscription.bicep' = {
  name: 'ingestionChangeDataRawSubscrption'
  params: {
    applicationName: applicationName
    env: env
    uniqueDeployId: uniqueDeployId
    postfixCount: postfixCount
    topicName: 'ingestion-change'
    topicSubscriptions: [
      {
        subscriptionName: 'ingestion-change-dataraw-subscription'
        labelFilter: dataRawStoredDataObjectTypeNames
      }
    ]
  }
  dependsOn: [serviceBusModule]
}

module ingestionFullbatchDataRawSubscrptionModule './shared_modules/dih-servicebus-topic-subscription.bicep' = {
  name: 'ingestionFullbatchDataRawSubscrption'
  params: {
    applicationName: applicationName
    env: env
    uniqueDeployId: uniqueDeployId
    postfixCount: postfixCount
    topicName: 'ingestion-fullbatch'
    topicSubscriptions: [
      {
        subscriptionName: 'ingestion-fullbatch-dataraw-subscription'
        labelFilter: dataRawStoredDataObjectTypeNames
      }
    ]
  }
  dependsOn: [serviceBusModule]
}

// Storage containers, tables
var containerNames = [for obj in ingestionDomainObjects: 'ingestion-${toLower(obj.DataObjectTypeName)}']
var tableNames = [
  'RawDataBatchesHandled'
  'RawDataUnchangedIds'
  'RawDataDeletedIds'
  'RawDataImportLog'
  'RawDataActiveBatches'
  'RawDataCanceledBatches'
  'RawDataChangesHandled'
]

module storageAccountModule './modules/storageaccount.bicep' = {
  name: 'storageAccountDeployment${applicationName}${env}${uniqueDeployId}' // Ensure unique deployment name
  params: {
    storageAccountName: sharedStorageAccountName
    containerNames: containerNames
    tableNames: tableNames
    location: location
    tags: tags
  }
}

// Configuration map
var configKeys = [
  // DIH Functions
  { keyName: 'DIH:Functions:MaxParallelTasks', keyValue: '20' }
  { keyName: 'DIH:Functions:ResourceIntensive:MaxParallelTasks', keyValue: '8' }
  { keyName: 'DIH:Functions:ExternalWebApi:MaxParallelTasks', keyValue: '4' }
  { keyName: 'DIH:Functions:MaxInMemObjects', keyValue: '500' }
  { keyName: 'DIH:Functions:MaxTasksPerMessage', keyValue: '250' }
  { keyName: 'DIH:Functions:BatchTimeoutSeconds', keyValue: '3600' }
  { keyName: 'DIH:Functions:MessageTTLSeconds', keyValue: '36000' }
  { keyName: 'DIH:Functions:CancelFullBatchOnException', keyValue: 'True' }

  // Ingestion Endpoints
  {
    keyName: 'Ingestion:StorageHttpEndpoint'
    keyValue: 'https://${sharedStorageAccountName}.blob.${environment().suffixes.storage}'
  }
  { keyName: 'Ingestion:Msg:ServiceBus:fullyQualifiedNamespace', keyValue: '${serviceBusName}.servicebus.windows.net' }

  // Ingestion Topic Names
  { keyName: 'Ingestion:IngestionChange:TopicName', keyValue: 'ingestion-change' }
  { keyName: 'Ingestion:IngestionFullbatch:TopicName', keyValue: 'ingestion-fullbatch' }

  // All known DataObjectTypeName
  {
    keyName: 'Ingestion:DataObjectTypeNames'
    keyValue: join(map(domainObjects.Ingestion, obj => obj.DataObjectTypeName), ',')
  }
  {
    keyName: 'Service:DataObjectTypeNames'
    keyValue: join(map(domainObjects.Service, obj => obj.DataObjectTypeName), ',')
  }

  // Data Raw Configuration
  {
    keyName: 'Data:Raw:StorageHttpEndpoint'
    keyValue: 'https://${sharedStorageAccountName}.blob.${environment().suffixes.storage}'
  }
  {
    keyName: 'Data:Raw:TableServiceEndpoint'
    keyValue: 'https://${sharedStorageAccountName}.table.${environment().suffixes.storage}'
  }
  { keyName: 'Data:Raw:ServiceBus:fullyQualifiedNamespace', keyValue: '${serviceBusName}.servicebus.windows.net' }
  { keyName: 'Data:Raw:CosmosAccountEndpoint', keyValue: 'https://${cosmosDbName}.documents.azure.com:443/' }
  { keyName: 'Data:Raw:HistoryRetentionDays', keyValue: '14' }
  { keyName: 'Data:Raw:MaxDeletePercent', keyValue: '100' }
  { keyName: 'Data:Raw:SoftDelete', keyValue: 'true' }
  { keyName: 'Data:Raw:SoftDeletedRetentionDays', keyValue: '14' }

  // Data Raw Table names
  { keyName: 'Data:Raw:Table:ActiveBatches', keyValue: 'RawDataActiveBatches' }
  { keyName: 'Data:Raw:Table:BatchesHandled', keyValue: 'RawDataBatchesHandled' }
  { keyName: 'Data:Raw:Table:CanceledBatches', keyValue: 'RawDataCanceledBatches' }
  { keyName: 'Data:Raw:Table:ChangesHandled', keyValue: 'RawDataChangesHandled' }
  { keyName: 'Data:Raw:Table:DeletedIds', keyValue: 'RawDataDeletedIds' }
  { keyName: 'Data:Raw:Table:ImportLog', keyValue: 'RawDataImportLog' }
  { keyName: 'Data:Raw:Table:UnchangedIds', keyValue: 'RawDataUnchangedIds' }

  // Raw Data Change Queue / Topic Names
  { keyName: 'Data:Raw:RawDataChange:TopicName', keyValue: 'dataraw-change' }
  { keyName: 'Data:Raw:ReceiveChange:SubscriptionName', keyValue: 'ingestion-change-dataraw-subscription' }
  { keyName: 'Data:Raw:ReceiveFullbatch:SubscriptionName', keyValue: 'ingestion-fullbatch-dataraw-subscription' }
  { keyName: 'Data:Raw:ReceiveFullBatch:QueueName', keyValue: 'dataraw-receive-fullbatch' }
  { keyName: 'Data:Raw:ReceiveFullBatchAbort:QueueName', keyValue: 'dataraw-receive-fullbatch-abort' }
  { keyName: 'Data:Raw:ReceiveFullBatchBroadcast:QueueName', keyValue: 'dataraw-receive-fullbatch-broadcast' }
  { keyName: 'Data:Raw:ReceiveFullBatchCleanup:QueueName', keyValue: 'dataraw-receive-fullbatch-cleanup' }
  { keyName: 'Data:Raw:ReceiveFullBatchPurgeExecute:QueueName', keyValue: 'dataraw-receive-fullbatch-purge-execute' }
  { keyName: 'Data:Raw:ReceiveFullBatchPurgePlan:QueueName', keyValue: 'dataraw-receive-fullbatch-purge-plan' }

  // Data Prepared Configuration
  { keyName: 'Data:Prepared:ServiceBus:fullyQualifiedNamespace', keyValue: '${serviceBusName}.servicebus.windows.net' }
  { keyName: 'Data:Prepared:CosmosAccountEndpoint', keyValue: 'https://${cosmosDbName}.documents.azure.com:443/' }
  {
    keyName: 'Data:Prepared:TableServiceEndpoint'
    keyValue: 'https://${sharedStorageAccountName}.table.${environment().suffixes.storage}'
  }
  { keyName: 'Data:Prepared:RawDataChange:SubscriptionName', keyValue: 'dataprep-subscription' }

  // Prepared Data Change Queue / Topic Names
  { keyName: 'Data:Prepared:PreparedDataChange:TopicName', keyValue: 'dataprepared-change' }

  // Service Configuration
  { keyName: 'Services:DefaultPageItemCount', keyValue: '400' }
  { keyName: 'Services:MaxPageItemCount', keyValue: '5000' }

  // Service Change Queue / Topic Names
  { keyName: 'Data:Service:PreparedDataChange:SubscriptionName', keyValue: 'service-subscription' }

  // Update Sentinel Key Value (will make apps refresh configuration within 5 minutes)
  { keyName: 'DIH:Config:Sentinel', keyValue: configSentinelValue }

  //Key Vault
  { keyName: 'GlobalKeyVault:Uri', keyValue: 'https://${keyVaultName}.vault.azure.net/' }
]

// Creating App Configuration settings from mao
resource appConfigurationKeys 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [
  for config in configKeys: {
    parent: appconfig
    name: config.keyName
    properties: {
      value: config.keyValue
    }
  }
]

// Config based on domainObjects
resource appConfigIngestionContainerNames 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [
  for ingestionObject in domainObjects.Ingestion: {
    parent: appconfig
    name: 'Ingestion:${ingestionObject.DataObjectTypeName}:StorageContainerName'
    properties: {
      value: 'ingestion-${toLower(ingestionObject.DataObjectTypeName)}'
    }
  }
]

resource appConfigIngestionChangeQueueNames 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [
  for ingestionObject in domainObjects.Ingestion: {
    parent: appconfig
    name: 'Ingestion:${ingestionObject.DataObjectTypeName}:Change:QueueName'
    properties: {
      value: 'ingestion-${toLower(ingestionObject.DataObjectTypeName)}-change'
    }
  }
]

resource appConfigIngestionFullbatchQueueNames 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [
  for ingestionObject in domainObjects.Ingestion: {
    parent: appconfig
    name: 'Ingestion:${ingestionObject.DataObjectTypeName}:Fullbatch:QueueName'
    properties: {
      value: 'ingestion-${toLower(ingestionObject.DataObjectTypeName)}-fullbatch'
    }
  }
]

resource appConfigRawPartitionKeys 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [
  for ingestionObject in domainObjects.Ingestion: {
    parent: appconfig
    name: 'Data:Raw:PartitionKey:${ingestionObject.DataObjectTypeName}'
    properties: {
      value: ingestionObject.PartitionKey
    }
  }
]

resource appConfigRawIdSubstitute 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [
  for ingestionObject in domainObjects.Ingestion: {
    parent: appconfig
    name: 'Data:Raw:IdSubstitute:${ingestionObject.DataObjectTypeName}'
    properties: {
      value: ingestionObject.IdSubstitute
    }
  }
]

resource appConfigPreparedPartitionKeys 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [
  for serviceObject in domainObjects.Service: {
    parent: appconfig
    name: 'Data:Prepared:PartitionKey:${serviceObject.DataObjectTypeName}'
    properties: {
      value: serviceObject.PartitionKey
    }
  }
]

resource preparedCopyFromRawNames 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = [
  for serviceObject in filter(domainObjects.Service, obj => obj.CopyFromRaw != null): {
    parent: appconfig
    name: 'Data:Prepared:${serviceObject.DataObjectTypeName}:CopyFromRaw'
    properties: {
      value: serviceObject.CopyFromRaw
    }
  }
]

// Output
output appInsightsInstrumentationKey string = appInsightsModule.outputs.appInsightsInstrumentationKey
output serviceBusNamespaceId string = serviceBusModule.outputs.serviceBusNamespaceId
