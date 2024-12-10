param tags object
param consistencyPolicy object = {
  defaultConsistencyLevel: 'Session'
}
param cosmosDbAccountName string
param cosmosDbNamePrepared string
param cosmosDbNameRaw string
param kind string = 'GlobalDocumentDB'
param location string
param offerType string = 'Standard'
param domainObjectsJson string

var domainObjects = json(domainObjectsJson)
var ingestionDomainObjects = [for obj in domainObjects.Ingestion: {
  DataObjectTypeName: obj.DataObjectTypeName
  IdSubstitute: obj.IdSubstitute
  PartitionKey: obj.PartitionKey
  StoreInRaw: obj.StoreInRaw
}]
var dataRawStoredIngestionDomainObjects = filter(ingestionDomainObjects, obj => obj.StoreInRaw == true)
var capabilities = [
  {
    name: 'EnableServerless'
  }
]
var locations = [
  {
    locationName: location
    failoverPriority: 0
    isZoneRedundant: false
  }
]

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-08-15' = {
  name: cosmosDbAccountName
  location: location
  tags: tags
  kind: kind
  properties: {
    databaseAccountOfferType: offerType
    consistencyPolicy: consistencyPolicy
    capabilities: capabilities
    locations: locations
    disableLocalAuth: false
  }
}

resource rawDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2022-05-15' = {
  parent: cosmosDbAccount
  name: cosmosDbNameRaw
  properties: {
    resource: {
      id: cosmosDbNameRaw
    }
  }
}

resource preparedDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2022-05-15' = {
  parent: cosmosDbAccount
  name: cosmosDbNamePrepared
  properties: {
    resource: {
      id: cosmosDbNamePrepared
    }
  }
}

resource rawContainers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2022-05-15' = [for obj in dataRawStoredIngestionDomainObjects: {
  parent: rawDatabase
  name: obj.DataObjectTypeName
  properties: {
    resource: {
      id: obj.DataObjectTypeName
      partitionKey: {
        paths: [
          '/${obj.PartitionKey}'
        ]
        kind: 'Hash'
      }
    }
    options: {}
  }
}]

resource preparedContainers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2022-05-15' = [for obj in domainObjects.Service: {
  parent: preparedDatabase
  name: obj.DataObjectTypeName
  properties: {
    resource: {
      id: obj.DataObjectTypeName
      partitionKey: {
        paths: [
          '/${obj.PartitionKey}'
        ]
        kind: 'Hash'
      }
    }
    options: {}
  }
}]

resource cosmosDbReaderWriterRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2021-05-15' = {
  parent: cosmosDbAccount
  name: guid(cosmosDbAccount.id, 'cosmosdb_reader_writer')
  properties: {
    roleName: 'cosmosdb_reader_writer'
    type: 'CustomRole'
    assignableScopes: [
      cosmosDbAccount.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/*'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/*'
        ]
      }
    ]
  }
}

resource cosmosDbReaderRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2021-05-15' = {
  parent: cosmosDbAccount
  name: guid(cosmosDbAccount.id, 'cosmosdb_reader')
  properties: {
    roleName: 'cosmosdb_reader'
    type: 'CustomRole'
    assignableScopes: [
      cosmosDbAccount.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/read'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/executeQuery'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/readChangeFeed'
        ]
      }
    ]
  }
}

