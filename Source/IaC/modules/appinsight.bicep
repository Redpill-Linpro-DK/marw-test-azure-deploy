param tags object

param appInsightsName string
param location string

var logname = replace(appInsightsName,'appi', 'log')

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2020-10-01' = {
  name: logname
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
  tags:tags
}

resource appInsightsComponents 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
  tags:tags
}

output appInsightsInstrumentationKey string = appInsightsComponents.properties.InstrumentationKey
