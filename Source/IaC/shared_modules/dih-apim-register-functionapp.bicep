param apimServiceName string
param functionAppName string
param functionAppResourceGroupName string
param functionAppSubscriptionId string = subscription().subscriptionId
param functionAppBasePath string
param apiName string
param apiPath string
param apiProductNames array = []
param apiDisplayName string = apiName
param apiDescription string = 'Exposing Azure Function App ${functionAppName}'
param apiSubscriptionRequired bool = true
param apiOperations array
param aadAppClientId string = '' // Optional Azure AD Application (client) ID for backend authentication
param azureAadTokenAppClientIds array = [] // Optional Azure AD Application (client) ID for OAuth validation - if not empty OAuth token from one of these apps must be presented

// Set API version for Microsoft.Web/sites reference
var functionAppApiVersion = '2022-03-01'

// Resolve variables
var functionAppResourceId = resourceId(functionAppSubscriptionId, functionAppResourceGroupName, 'Microsoft.Web/sites', functionAppName)
var functionAppReference = reference(functionAppResourceId, functionAppApiVersion)
var functionAppUrl = 'https://${functionAppReference.defaultHostName}${functionAppBasePath}'
var backendName = '${apiName}-backend'

// HTTP Responses used if not explicitly defined on an operation
var defaultHttpResponses = [
  {
    statusCode: 200
    description: 'Successful response'
    representations: [ { contentType: 'application/json' } ]
  }
  {
    statusCode: 400
    description: 'Bad Request'
    representations: [ { contentType: 'application/json' } ]
  }
  {
    statusCode: 500
    description: 'Server Error'
    representations: [ { contentType: 'application/json' } ]
  }
]

// Existing APIM service
resource apimService 'Microsoft.ApiManagement/service@2023-05-01-preview' existing = {
  name: apimServiceName
}

// Create API
resource apiDefinition 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  parent: apimService
  name: apiName
  properties: {
    displayName: apiDisplayName
    description: apiDescription
    subscriptionRequired: apiSubscriptionRequired
    serviceUrl: functionAppUrl
    path: apiPath
    protocols: [
      'https'
    ]
  }
}

// Existing APIM Product references
resource apiProducts 'Microsoft.ApiManagement/service/products@2023-05-01-preview' existing = [
  for (apiProductName, i) in apiProductNames: {
    parent: apimService
    name: apiProductName
}]

// Set product links
resource apiProductLinks 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = [
  for (productName, i) in apiProductNames: {
    parent: apiProducts[i]
    name: apiName
    dependsOn: [
      apiDefinition
      apiProducts[i]
    ]
}]

// Set backend
resource apiBackend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = {
  parent: apimService
  name: backendName
  properties: {
    url: functionAppUrl
    protocol: 'http'
    // resourceId: functionAppResourceId // Reference the Function App's resource ID
  }
}

// Create API operations for each specified path, method, and response configuration
resource apiOperationsResource 'Microsoft.ApiManagement/service/apis/operations@2023-05-01-preview' = [
  for (operation, i) in apiOperations: {
    parent: apiDefinition
    name: '${apiName}-operation-${i}'
    properties: {
      displayName: operation.?displayName != null ? operation.displayName : '${operation.method} ${operation.path}'
      method: operation.method
      urlTemplate: operation.path
      description: operation.description
      templateParameters: operation.?templateParameters ?? []
      request: {
        queryParameters: operation.?queryParameters ?? []
      }
      responses: operation.?responses ?? defaultHttpResponses
    }
  }
]

// Define the rewrite policy for operations that have rewriteUri
 resource apiOperationPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2023-05-01-preview' = [
   for (operation, i) in apiOperations: if (contains(operation,'rewriteUri')) {
     parent: apiOperationsResource[i] // Reference the specific operation resource using [i]
     name: 'policy'
     properties: {
       format: 'xml'
       value: '<policies><inbound><base /><rewrite-uri template="${operation.rewriteUri}" /></inbound><backend><forward-request /></backend><outbound /><on-error /></policies>'
     }
   }
 ]

// Construct the inbound authentication policy, if azureAadTokenAppClientIds is provided
var clientAppIdsXml = join(map(azureAadTokenAppClientIds, id => '<application-id>${id}</application-id>'),'')
var inboundAuthPolicy = !empty(azureAadTokenAppClientIds) ? '<validate-azure-ad-token tenant-id="${subscription().tenantId}"><client-application-ids>${clientAppIdsXml}</client-application-ids></validate-azure-ad-token>' : ''

// Construct the backend authentication policy, if aadAppClientId is provided
var backendAuthPolicy = !empty(aadAppClientId) ? '<authentication-managed-identity resource="${aadAppClientId}" output-token-variable-name="msi-access-token" ignore-error="false" /><set-header name="Authorization" exists-action="override"><value>@("Bearer " + (string)context.Variables["msi-access-token"])</value></set-header>' : ''

// Construct the complete policy XML
var completePolicy = '<policies><inbound><base />${inboundAuthPolicy}<set-backend-service base-url="${functionAppUrl}" />${backendAuthPolicy}</inbound><backend><forward-request /></backend><outbound /><on-error /></policies>'

// Set API policies (e.g., CORS, backend forwarding, etc.)
resource apiDefinition_policy 'Microsoft.ApiManagement/service/apis/policies@2023-05-01-preview' = {
  parent: apiDefinition
  name: 'policy'
  properties: {
    format: 'xml'
    value: completePolicy
  }
}

// Give APIM role "Function App Contributer" to the function apps resource group (so it can authenticate)
module roleFunctionAppContributer './dih-apim-register-functionapp-permissions.bicep' = {
  name: 'roleFunctionAppContributer'
  scope: resourceGroup(functionAppResourceGroupName)
  params: {
    apimServiceName: apimServiceName
    commonResourceGroupName: resourceGroup().name
    functionAppName: functionAppName
  }
}
