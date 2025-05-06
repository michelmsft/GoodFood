@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}
param privateIP string

@description('Id of the user or app to assign application roles')
param principalId string

var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)
var cosmos_acc_name=  'cosmos-${resourceToken}'
var openAIName = 'openai-${resourceToken}'
var speechServiceName = 'speech-${resourceToken}'
var sperimeters_acc_name = 'sp-${resourceToken}'

//gpt-35-turbo and gpt-4o-mini
var deployments = [
  {
    name: 'gpt-35-turbo'
    model: {
            format: 'OpenAI'
            name: 'gpt-35-turbo'
            version: '0125'
    }
    raiPolicyName: 'openai-policy'
  }
]

@description('Creates an Azure OpenAI resource.')
module openAI './modules/cognitive.bicep' = {
  name: openAIName
  params: {
    location: location
    name: openAIName
    deployments: deployments
    tags: tags
    allowedIpRules: [
      {
        value: privateIP
      }
    ]
  }
}

@description('Creates an Azure AI Services Speech service.')
resource speechService 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: speechServiceName
  location: location
  kind: 'SpeechServices'
  tags: tags
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: speechServiceName
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Deny'
      ipRules: [
        {
          value: privateIP
        }
      ]
    }
  }
}


resource cosmosdb 'Microsoft.DocumentDB/databaseAccounts@2024-12-01-preview' = {
  name: cosmos_acc_name
  location: location
  tags: union(tags, { 
    defaultExperience: 'Core (SQL)'
    'hidden-workload-type': 'Learning'
    'hidden-cosmos-mmspecial': ''
  })
  kind: 'GlobalDocumentDB'
  identity: {
    type: 'None'
  }
  properties: {
    publicNetworkAccess: 'Disabled'
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    isVirtualNetworkFilterEnabled: false
    virtualNetworkRules: []
    disableKeyBasedMetadataWriteAccess: false
    enableFreeTier: false
    enableAnalyticalStorage: false
    analyticalStorageConfiguration: {
      schemaType: 'WellDefined'
    }
    databaseAccountOfferType: 'Standard'
    enableMaterializedViews: false
    capacityMode: 'Serverless'
    defaultIdentity: 'FirstPartyIdentity'
    networkAclBypass: 'None'
    disableLocalAuth: false
    enablePartitionMerge: false
    enablePerRegionPerPartitionAutoscale: false
    enableBurstCapacity: false
    enablePriorityBasedExecution: false
    minimalTlsVersion: 'Tls12'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
      maxIntervalInSeconds: 5
      maxStalenessPrefix: 100
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    cors: []
    capabilities: []
    ipRules: []
    backupPolicy: {
      type: 'Periodic'
      periodicModeProperties: {
        backupIntervalInMinutes: 240
        backupRetentionIntervalInHours: 8
        backupStorageRedundancy: 'Geo'
      }
    }
    networkAclBypassResourceIds: []
    diagnosticLogSettings: {
      enableFullTextQuery: 'None'
    }
    capacity: {
      totalThroughputLimit: 4000
    }
  }
}


resource cosmosdb_db 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-12-01-preview' = {
  parent: cosmosdb
  name: 'goodfooddb'
  properties: {
    resource: {
      id: 'goodfooddb'
    }
  }
}

resource cosmosdb_db_events 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: cosmosdb_db
  name: 'events'
  properties: {
    resource: {
      id: 'events'
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/streamid'
        ]
        kind: 'Hash'
        version: 2
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      computedProperties: []
    }
  }
}

resource cosmosdb_db_views 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-12-01-preview' = {
  parent: cosmosdb_db
  name: 'views'
  properties: {
    resource: {
      id: 'views'
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      partitionKey: {
        paths: [
          '/streamid'
        ]
        kind: 'Hash'
        version: 2
      }
      uniqueKeyPolicy: {
        uniqueKeys: []
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
      computedProperties: []
    }
  }
}

resource appendToStreamStoredProcs 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/storedProcedures@2021-06-15' = {
  parent: cosmosdb_db_events
  name: 'SpAppendToStream'
  properties: {
    resource: {
      id: 'SpAppendToStream'
      body: '''
      function appendToStream(streamId, event) {
        try {
            var versionQuery = {
                'query': 'SELECT VALUE Max(e.version) FROM events e WHERE e.streamid = @streamId',
                'parameters': [{ 'name': '@streamId', 'value': streamId }]
            };
    
            const isAccepted = __.queryDocuments(__.getSelfLink(), versionQuery,
                function (err, items, options) {
                    if (err) {
                        __.response.setBody({ error: "Query Failed: " + err.message });
                        return;
                    }
    
                    var currentVersion = (items && items.length && items[0] !== null) ? items[0] : -1;
                    var newVersion = currentVersion + 1;
    
                    event.version = newVersion;
                    event.streamid = streamId;
    
                    const accepted = __.createDocument(__.getSelfLink(), event, function (err, createdDoc) {
                        if (err) {
                            __.response.setBody({ error: "Insert Failed: " + err.message });
                            return;
                        }
                        __.response.setBody(createdDoc);
                    });
    
                    if (!accepted) {
                        __.response.setBody({ error: "Insertion was not accepted." });
                    }
                });
    
            if (!isAccepted) __.response.setBody({ error: "The query was not accepted by the server." });
        } catch (e) {
            __.response.setBody({ error: "Unexpected error: " + e.message });
        }
      }'''
    }
  }
}


resource cosmos_role1 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2024-12-01-preview' = {
  parent: cosmosdb
  name: '00000000-0000-0000-0000-000000000001'
  properties: {
    roleName: 'Cosmos DB Built-in Data Reader'
    type: 'BuiltInRole'
    assignableScopes: [
      cosmosdb.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/executeQuery'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/readChangeFeed'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/read'
        ]
        notDataActions: []
      }
    ]
  }
}

resource cosmos_role2 'Microsoft.DocumentDB/databaseAccounts/sqlRoleDefinitions@2024-12-01-preview' = {
  parent: cosmosdb
  name: '00000000-0000-0000-0000-000000000002'
  properties: {
    roleName: 'Cosmos DB Built-in Data Contributor'
    type: 'BuiltInRole'
    assignableScopes: [
      cosmosdb.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/*'
          'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers/items/*'
        ]
        notDataActions: []
      }
    ]
  }
}

resource table_role1 'Microsoft.DocumentDB/databaseAccounts/tableRoleDefinitions@2024-12-01-preview' = {
  parent: cosmosdb
  name: '00000000-0000-0000-0000-000000000001'
  properties: {
    roleName: 'Cosmos DB Built-in Data Reader'
    type: 'BuiltInRole'
    assignableScopes: [
      cosmosdb.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/executeQuery'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/readChangeFeed'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/entities/read'
        ]
        notDataActions: []
      }
    ]
  }
}

resource table_role2 'Microsoft.DocumentDB/databaseAccounts/tableRoleDefinitions@2024-12-01-preview' = {
  parent: cosmosdb
  name: '00000000-0000-0000-0000-000000000002'
  properties: {
    roleName: 'Cosmos DB Built-in Data Contributor'
    type: 'BuiltInRole'
    assignableScopes: [
      cosmosdb.id
    ]
    permissions: [
      {
        dataActions: [
          'Microsoft.DocumentDB/databaseAccounts/readMetadata'
          'Microsoft.DocumentDB/databaseAccounts/tables/*'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/*'
          'Microsoft.DocumentDB/databaseAccounts/tables/containers/entities/*'
        ]
        notDataActions: []
      }
    ]
  }
}

resource sperimeters 'Microsoft.Network/networkSecurityPerimeters@2023-08-01-preview' = {
  name: sperimeters_acc_name
  location: location
  properties: {}
}

resource sperimeters_profile 'Microsoft.Network/networkSecurityPerimeters/profiles@2023-08-01-preview' = {
  parent: sperimeters
  name: 'userProfile'
  location: location
  properties: {}
}

resource sperimeters_profile_LocalAddress 'Microsoft.Network/networkSecurityPerimeters/profiles/accessRules@2023-08-01-preview' = {
  parent: sperimeters_profile
  name: 'LocalAddress'
  properties: {
    direction: 'Inbound'
    addressPrefixes: [
      privateIP
    ]
    fullyQualifiedDomainNames: []
    subscriptions: []
    emailAddresses: []
    phoneNumbers: []
  }
}

resource spcosmos 'Microsoft.Network/networkSecurityPerimeters/resourceAssociations@2023-08-01-preview' = {
  parent: sperimeters
  name: '${sperimeters_acc_name}cosmos'
  properties: {
    privateLinkResource: {
      id: cosmosdb.id
    }
    profile: {
      id: sperimeters_profile.id
    }
    accessMode: 'Learning'
  }
}


output OPENAI_RESOURCE_ID string = openAI.outputs.id
output SPEECH_SERVICE_URI string = 'https://${speechServiceName}.cognitiveservices.azure.com'
output SPEECH_SERVICE_RESOURCE_ID string = speechService.id

output COSMOS_URI string = cosmosdb.properties.documentEndpoint
output COSMOS_DB_URI string = 'https://${cosmos_acc_name}.documents.azure.com:443/'
output OPENAI_SERVICE_URI string = openAI.outputs.endpoint

output COSMOS_NAME string = cosmos_acc_name
output COSMOS_ROLE1 string = cosmos_role1.id
output COSMOS_ROLE2 string = cosmos_role2.id
output USER string = principalId
