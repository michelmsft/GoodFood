targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string


@description('Current IP')
param IP string = ''

@description('Id of the user or app to assign application roles')
param principalId string

// Tags that should be applied to all resources.
// 
// Note that 'azd-service-name' tags should be applied separately to service host resources.
// Example usage:
//   tags: union(tags, { 'azd-service-name': <service name in azure.yaml> })
var tags = {
  'azd-env-name': environmentName
  SecurityControl: 'Ignore'
}

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    tags: tags
    principalId: principalId
    privateIP: IP
  }
}



output OPENAI_RESOURCE_ID string = resources.outputs.OPENAI_RESOURCE_ID
output OPENAI_SERVICE_URI string = resources.outputs.OPENAI_SERVICE_URI
output SPEECH_SERVICE_URI string = resources.outputs.SPEECH_SERVICE_URI
output SPEECH_SERVICE_RESOURCE_ID string = resources.outputs.SPEECH_SERVICE_RESOURCE_ID


output COSMOS_URI string = resources.outputs.COSMOS_URI

output USER string = resources.outputs.USER
output COSMOS_NAME string =  resources.outputs.COSMOS_NAME
output COSMOS_ROLE1 string = resources.outputs.COSMOS_ROLE1
output COSMOS_ROLE2 string = resources.outputs.COSMOS_ROLE2
output RGNAME string = rg.name
