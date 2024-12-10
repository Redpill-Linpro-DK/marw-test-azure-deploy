# Azure Function App Integration with Azure API Management (APIM) Module

This Bicep module automates the integration of an Azure Function App with an Azure API Management (APIM) service. It creates and configures API operations on APIM that map 1:1 with the endpoints (sub-paths) of the Function App. The module handles the creation of API operations, links them with APIM products, and sets up backend configuration for secure communication between APIM and the Function App.

This module must be called with `scope: resourceGroup(commonResourcegroupName)`such that in runs in the scope of the resource group that the APIM instance exists in, see call example below.

## Overview

The module is designed to simplify the process of exposing Function App endpoints through APIM by automating the setup of API operations, including handling authentication via Azure AD if needed. It supports the definition of multiple API operations with their own query parameters and response configurations.

### Parameters

- **`apimServiceName` (string):**  
  The name of the existing APIM service where the API operations will be created.

- **`functionAppName` (string):**  
  The name of the Function App that is being exposed through APIM.

- **`functionAppResourceGroupName` (string):**  
  The resource group name where the Function App resides.

- **`functionAppSubscriptionId` (string):**  
  The subscription ID where the Function App is deployed. Defaults to the current subscription ID.

- **`functionAppBasePath` (string):**  
  The base path of the Function App (typically `/api` that will be used by APIM when mapping operation paths to paths in for function app.

- **`apiName` (string):**  
  The name (id) of the API to be created in APIM. 

- **`apiDisplayName` (string):**  
  The display name for the API in APIM. Defaults to the value of `apiName`.

- **`apiPath` (string):**  
  The base path for the API in APIM. All your operation paths will exists below this path.

- **`apiProductNames` (array):**  
  A list of existing APIM product names to which this API will be linked. You can use this to control access to the API.

- **`apiDescription` (string):**  
  A description for the API in APIM. Defaults to "Exposing Azure Function App ${functionAppName}".

- **`apiSubscriptionRequired` (bool):**  
  A flag indicating whether subscriptions are required for this API. Defaults to `true`. When false unauthenticated users can call it.

- **`apiOperations` (array):**  
  An array of objects defining the operations to be created in APIM. Each object should have the following structure:
  - **`displayName` (string):** (Optional) The display name for the operation. Defaults to the value of method + path
  - **`path` (string):** The path for the operation relative to `apiPath`.
  - **`method` (string):** The HTTP method (e.g., GET, POST) for the operation.
  - **`description` (string):** A description of the operation.
  - **`rewriteUri` (string):** (Optional) Value for overwriting the backend Uri. 
  - **`templateParameters` (array):** (Optional) An array of template parameters for the operation. This field is optional and defaults to no query parameters if not specified. See below for details.
  - **`queryParameters` (array):** (Optional) An array of query parameters for the operation. This field is optional and defaults to no query parameters if not specified. See below for details.  - **`responses` (array):** (Optional) An array of response objects. This field is optional and defaults to responses with status codes 200, 400, and 500 if not specified. See below for details.

- **`aadAppClientId` (string):**  
  (Optional) The Azure AD Application (client) ID for backend authentication if using managed identities.

- **`azureAadTokenAppClientIds` (array):**  
  (Optional) Array of strings (Client IDs, GUIDs) that represent AAD App IDs. If present the API will be protected using OAuth. Caller should provide a valid token generated from one of the provided App IDs.

## Authenticating callers

With this template you can require auth by using one of the parameters `apiSubscriptionRequired` (a Subscription Key must be provided via query or header)  or `azureAadTokenAppClientIds` (a JWT OAuth token generated using one of the provided App IDs must be provided).

Subscription keys can be obtained via the Azure Portal and the APIM Developer Portal (self service for users).

For OAuth JWT generation you need to know the tenant id, app id and app secret. A token can be generated from `https://login.microsoftonline.com/{tenant id}/oauth2/v2.0/token` POST providing the following parameters via body (x-www-form-urlencoded): scope = https://graph.microsoft.com/.default, grant_type = client_credentials, client_id = client id as provided in  azureAadTokenAppClientIds param, client_secret = ... (it's a secret)

## Example Usage

This example is expected to run in the context of the Function App's Resource Group. The APIM instance is expected to exist in the common resource group.

```bicep
var apimServiceName = toLower('apim-${applicationName}-${env}-${uniqueDeployId}-${postfixCount}')
var commonResourcegroupName = toLower('rg-${applicationName}-c-${env}-${postfixCount}')

// Function app
module functionAppModule '../../../DIH.AVK.Common/Source/IaC/shared_modules/dih-functionapp.bicep' = {
  name: 'functionApp'
  params: {
    // ... your existing function app - already in your ingestion/service project's main.bicep ...
  }
}

module registerFunctionAppApi '../../../DIH.AVK.Common/Source/IaC/shared_modules/dih-apim-register-functionapp.bicep' = {
  name: 'registerFunctionAppApi'
  scope: resourceGroup(commonResourcegroupName)
  params: {
    apimServiceName: apimServiceName
    functionAppResourceGroupName: resourceGroup().name
    functionAppName: functionAppModule.outputs.functionAppName
    functionAppBasePath: '/api'
    apiName: 'material-handling'
    apiPath: 'material-handling'
    apiProductNames: ['material-handling-product']
    apiSubscriptionRequired: true
    apiDisplayName: 'Material Handling API'
    apiDescription: 'API for material handling operations'
    apiOperations: [
      {
        path: '/mhax-outbound-event'
        method: 'POST'
        description: 'Post MHAX event, signaling new Material Handling task'
        queryParameters: []  // No query parameters for this operation
        responses: [
          {
            statusCode: 202
            description: 'Accepted'
            representations: [ { contentType: 'application/json' } ]
          }
          {
            statusCode: 400
            description: 'Bad Request'
            representations: [ { contentType: 'application/json' } ]
          }
          {
            statusCode: 500
            description: 'Internal Server Error'
            representations: [ { contentType: 'application/json' } ]
          }
        ]
      }
      {
        path: '/mhax-outbound-event'
        method: 'GET'
        description: 'Get MHAX event status'
        queryParameters: [
          {
            name: 'eventId'
            description: 'The ID of the event to retrieve'
            type: 'string'
            required: true
          }
          {
            name: 'includeDetails'
            description: 'Whether to include event details'
            type: 'bool'
            required: false
            defaultValue: 'false'
          }
        ]
      }
      {
        displayName: 'Any'
        path: '{type}'
        method: 'GET'
        description: 'Get data from Prepared'
        rewriteUri: '/Inventory?Query=id=p13 AND WarehouseID = Stockholm'
        templateParameters: [
		      {
			    name: 'type'
			    description: 'Type of data to fetch'
			    type: 'string'
			    required: true
		      }
		    ]
        queryParameters: [
          {
            name: 'Query'
            description: 'Query to receive data "id=p13 AND WarehouseID = Stockholm"'
            type: 'string'
            required: false
          }
        ]
      }
      {
        path: '/check-status'
        method: 'GET'
        description: 'Check the status of the system'
      }      
    ]
  }
  dependsOn: [functionAppModule] // this is not required - but make sure functionAppModule completed, if using in same main.bicep
}
```

In the above example these 3 mapping from APIM to Function App are defined:

- APIM path `/material-handling/mhax-outbound-event` mapping to `/api/mhax-outbound-event` for POST requests (no query strings)
- APIM path `/material-handling/mhax-outbound-event` mapping to `/api/mhax-outbound-event` for GET requests (2 query strings, eventId and includeDetails)
- APIM path `/material-handling/check-status` mapping to `/api/check-status` for GET requests (no query strings)

## Query Parameters

- **`queryParameters` array:**  
  Each operation can have an array of query parameters with the following fields:
  - **`name` (string):** The name of the parameter (e.g., `eventId`).
  - **`description` (string):** (Optional) A brief description of the parameter's purpose.
  - **`type` (string):** The data type (`string`, `int`, `bool`, etc.). Defaults to `string`.
  - **`required` (bool):** Indicates if the parameter is required. Defaults to `false`.
  - **`defaultValue` (string):** (Optional) The default value if not provided by the caller.

  More fields are available. For full details, refer to the official documentation, see `queryParameters` on https://learn.microsoft.com/en-us/azure/templates/microsoft.apimanagement/service/apis/operations?pivots=deployment-language-bicep#resource-format

## Template Parameters

- **`templateParameters` array:**  
  Each operation can have an array of query parameters with the following fields:
  - **`name` (string):** The name of the parameter (e.g., `eventId`).
  - **`description` (string):** (Optional) A brief description of the parameter's purpose.
  - **`type` (string):** The data type (`string`, `int`, `bool`, etc.). Defaults to `string`.
  - **`required` (bool):** Indicates if the parameter is required. Defaults to `false`.
  - **`defaultValue` (string):** (Optional) The default value if not provided by the caller.

  More fields are available. For full details, refer to the official documentation, see `templateParameters` on https://learn.microsoft.com/en-us/azure/templates/microsoft.apimanagement/service/apis/operations?pivots=deployment-language-bicep#resource-format

## Responses

- **`responses` array:**  
  You can specify multiple responses for each operation, defining different HTTP status codes and response formats. Each response object can have:
  - **`statusCode` (int):** The HTTP status code (e.g., 200, 400, 500).
  - **`description` (string):** A description of the response.
  - **`representations` (array):** An array of representations. Each representation should include a `contentType` (e.g., `application/json`).

  More fields are available. For full details, refer to the official documentation, see `responses` on https://learn.microsoft.com/en-us/azure/templates/microsoft.apimanagement/service/apis/operations?pivots=deployment-language-bicep#resource-format
