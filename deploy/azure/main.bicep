// cmind on Azure Container Apps: Web + MCP + Postgres Flexible Server.
// Node agents need a privileged Docker runtime, which Container Apps does not provide — run them
// on AKS (deploy/helm) or a VM/VMSS and point NodeAgent__MainUrl at the Web app URL.
//
//   az group create -n cmind-rg -l westeurope
//   az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
//     -p ownerEmail=you@example.com ownerPassword=... pgPassword=... imageRegistry=ghcr.io/your-org

@description('Deployment location')
param location string = resourceGroup().location

@description('Prefix for resource names')
param namePrefix string = 'cmind'

@description('Container image registry + repo prefix, e.g. ghcr.io/your-org/cmind')
param imageRegistry string

@description('Image tag')
param imageTag string = 'latest'

@secure()
param pgPassword string
param ownerEmail string
@secure()
param ownerPassword string
@secure()
param discoveryJoinToken string

@description('Optional OTLP endpoint to ALSO export logs/traces/metrics to a collector (leave empty to use Azure Monitor only)')
param otlpEndpoint string = ''

var pgAdmin = 'cmindadmin'
var connectionString = 'Host=${pg.properties.fullyQualifiedDomainName};Port=5432;Database=appdb;Username=${pgAdmin};Password=${pgPassword};SSL Mode=Require;Trust Server Certificate=true'

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-appi'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource pg 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
  name: '${namePrefix}-pg'
  location: location
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    version: '16'
    administratorLogin: pgAdmin
    administratorLoginPassword: pgPassword
    storage: { storageSizeGB: 32 }
    highAvailability: { mode: 'Disabled' }
  }
}

resource pgDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: pg
  name: 'appdb'
}

resource pgAllowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = {
  parent: pg
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-web'
  location: location
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      ingress: { external: true, targetPort: 8080, transport: 'auto' }
      secrets: [
        { name: 'connstr', value: connectionString }
        { name: 'owner-password', value: ownerPassword }
        { name: 'join-token', value: discoveryJoinToken }
        { name: 'appi-conn', value: appInsights.properties.ConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: '${imageRegistry}-web:${imageTag}'
          resources: { cpu: json('1.0'), memory: '2Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ConnectionStrings__appdb', secretRef: 'connstr' }
            { name: 'App__OwnerEmail', value: ownerEmail }
            { name: 'App__OwnerPassword', secretRef: 'owner-password' }
            { name: 'App__Discovery__Enabled', value: 'true' }
            { name: 'App__Discovery__JoinToken', secretRef: 'join-token' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'appi-conn' }
            { name: 'OTEL_EXPORTER_OTLP_ENDPOINT', value: otlpEndpoint }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 4 }
    }
  }
}

resource mcp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-mcp'
  location: location
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      ingress: { external: true, targetPort: 8080, transport: 'auto' }
      secrets: [
        { name: 'connstr', value: connectionString }
        { name: 'appi-conn', value: appInsights.properties.ConnectionString }
      ]
    }
    template: {
      containers: [
        {
          name: 'mcp'
          image: '${imageRegistry}-mcp:${imageTag}'
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ConnectionStrings__appdb', secretRef: 'connstr' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'appi-conn' }
            { name: 'OTEL_EXPORTER_OTLP_ENDPOINT', value: otlpEndpoint }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 3 }
    }
  }
}

output webUrl string = 'https://${web.properties.configuration.ingress.fqdn}'
output mcpUrl string = 'https://${mcp.properties.configuration.ingress.fqdn}/mcp'
output appInsightsName string = appInsights.name
