# Fast Food Drive thru Operator Ai Agent with Semantic Kernel and GPT 3.5 Turbo
### **Fast Food Drive-Thru Operator AI Agent**  

This AI-powered **Fast Food Drive-Thru Operator** leverages **Semantic Kernel** and **Azure OpenAI GPT-3.5 turbo** to streamline customer interactions, improve efficiency, and automate order processing.  

### **How It Works**  
1. **Speech & AI Interaction** – The system uses **Azure OpenAI GPT-3.5 turbo** with **Semantic Kernel Planner** for intelligent conversation and order handling.  
2. **Database Management** – Order details are stored and retrieved using **Cosmos DB for NoSQL**.  
3. **Voice Responses** – A **.NET Console App** integrates **System Speech Synthesis** to communicate with customers.  
4. **Custom Plugins** – C# plugins handle database CRUD operations for seamless transactions.

![image](https://github.com/user-attachments/assets/e61f1162-93d1-4847-a57a-bbb6de614d34)

This AI agent enhances the **drive-thru experience** with fast, intelligent, and voice-driven automation! 

## Phase 1: Backend Database for Fast Food ops

### Step 0: Download Install Cosmos DB for NoSQL Emulator 
for the sake of this example, we are going to use a local Cosmos DB for NoSQL Emulator as our backend DB. 
 
Go to the official download page: https://aka.ms/cosmosdb-emulator 
go to https://localhost:8081/_explorer/index.html and make sure it up running. in your local cosmos db emulator, you will create the following components:

1. Create a database named goodfooddb
2. Create a container named `events` with `/streamid` as partition key.
3. Create another container named `views` with `/streamid` as partition key.
4. Create a stored procedure for the the `events` container. Name the stored procedure  `SpAppendToStream`.
   The definition of the stored procedure is as follows:
   ``` javascript
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
    }

    ```
You can absolutely deploy your event sourcing backend database to your own Cosmos DB for NoSQL account in Azure if that works best for you. You can use Azure CLI command sequence to create an Azure Cosmos DB account, a database named `goodfooddb`, and two containers (`events` and `views`) with their respective partition keys and the stored procedure for the container `events`.

## Phase 2: FrontEnd Fast Food ops using a .Net Console app 

### Step-by-Step Guide to Implementing GoodFood Virtual Drive-Thru Assistant

#### Prerequisites

1. **Install Required Packages**:

You will install the required .net packages  and import the following libraries to your console app program.cs code.

- dotnet add package Microsoft.CognitiveServices.Speech 1.43.0
- dotnet add package Microsoft.SemanticKernel 1.35.0
- dotnet add package Microsoft.Azure.Cosmos 3.36.0
- dotnet add package Microsoft.Extensions.Configuration 9.0.3
- dotnet add package Microsoft.Extensions.Configuration.json 9.0.3
- dotnet add package Microsoft.Extensions.Caching.Memory 9.0.3
     
2. **import libraries**:

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Azure.Cosmos;
using System.ComponentModel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Azure;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using static GoodFoodPlugin;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.CognitiveServices.Speech;
using System.Drawing;
using static System.Net.Mime.MediaTypeNames;
```  
   
  
3. **Set the appsettings.json**

You'll configure the Azure OpenAI GPT-3.5 Turbo model by setting its deployment endpoint, name, and keys in the appsettings. You'll also need to provide your Azure AI Speech service region and key. The Cosmos DB for NoSQL endpoint and key are set to use the emulator by default. If you're using your own Cosmos DB account, be sure to update these values accordingly.

```json
{
   "ApiSettings": {
     "ApiKey": "PROVIDE_YOUR_OWN_GPT_3_5_TURBO_KEY",
     "ApiEndPointUrl": "YOUR_OWN_GPT_35_TURBO_DEPLOYMENT_ENDPOINT",
     "ApiModelName": "gpt-35-turbo",
     "SpeechServiceEndPoint": "PROVIDE_YOUR_OWN_AI_SPEECH_SERVICE_ENDPOINT",
     "SpeechServiceKey": "PROVIDE_YOUR_OWN_AI_SPEECH_SERVICE_KEY",
     "SpeechServiceRegion": "PROVIDE_YOUR_OWN_AI_SPEECH_SERVICE_REGION"
   },
   "CosmosDbSettings": {
     "CosmosDbUrl": "https://localhost:8081",
     "CosmosDbKey": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
   }
}
```
   
#### Step 1: Load Credential Data from `appsettings.json`

In this step, we load service credentials and configuration values from the appsettings.json file into variables so they can be used throughout the application.

```csharp
// Get the root directory of the application
string rootPath = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName
                    ?? AppContext.BaseDirectory;

// Load configuration from appsettings.json in the root folder
var config = new ConfigurationBuilder()
    .SetBasePath(rootPath) // Set the base path to the root directory
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();


// Retrieve values from the configuration
string? apiKey = config["ApiSettings:ApiKey"];
string? apiEndPointUrl = config["ApiSettings:ApiEndPointUrl"];
string? apiModelName = config["ApiSettings:ApiModelName"];
string? SpeechApiKey = config["ApiSettings:SpeechServiceKey"];
string? SpeechApiRegion = config["ApiSettings:SpeechServiceRegion"];
string? cosmosdbUrl = config["CosmosDbSettings:CosmosDbUrl"];
string? cosmosdbKey = config["CosmosDbSettings:CosmosDbKey"];

if (string.IsNullOrEmpty(apiEndPointUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiModelName))
{
    Console.WriteLine("Please check your appsettings.json file for missing or incorrect values.");
    return;
}
```

#### Step 2: Build the Kernel

In this step, we create and configure the Semantic Kernel by connecting it to the Azure OpenAI GPT model using the deployment name, endpoint, and API key. Once built, we retrieve the chat completion service from the kernel to enable AI-powered interactions.

```csharp
// Create a kernel with Azure OpenAI chat completion
IKernelBuilder builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(
    deploymentName: apiModelName,
    endpoint: apiEndPointUrl,
    apiKey: apiKey
);

// Build the kernel
Kernel kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
```
#### Step 3: Add a Plugin

In this step, we add a custom plugin to the kernel. This plugin (GoodFoodPlugin) is registered under the name "DriveThru" and provides the functionality needed for our Fast food drive thru application’s logic.

```csharp
// Add a plugin 
kernel.Plugins.AddFromType<GoodFoodPlugin>("DriveThru");
```
#### Step 4: Enable Planning

In this step, we configure prompt execution settings to guide the AI's behavior. By setting `FunctionChoiceBehavior` to `Auto()` and adjusting the Temperature, we enable the planner to make smarter decisions when selecting functions to execute.

```csharp
AzureOpenAIPromptExecutionSettings settings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    Temperature = 0.3
};
```
#### Step 5: Instantiate Messaging and Chat

In this step, we set up the main chat loop. The app captures the user's voice input using Azure Speech Recognition, sends it to the AI for processing, and then reads the response back using text-to-speech. The full conversation is tracked using chatHistory, enabling context-aware replies from the AI.

```csharp
string? userInput=null;
do
{
    // using speech recorgnition to retrieve user input

    Console.Write($"You :");
    var speechConfig = SpeechConfig.FromSubscription(SpeechApiKey, SpeechApiRegion);
    var recognizer = new SpeechRecognizer(speechConfig);

    int _retry = 0;
    var Audiotranscript = await recognizer.RecognizeOnceAsync();

    if (Audiotranscript.Reason == ResultReason.RecognizedSpeech)
    {
        userInput = Audiotranscript.Text;
        Console.Write($" {userInput}\n");
        chatHistory.AddUserMessage(userInput);
    }
    else if (Audiotranscript.Reason == ResultReason.NoMatch)
    {
        if (_retry == 3)
        {
            userInput = null;
        }
        else
        {
            userInput = "Hello!";
            _retry++;
        }
        continue;
    }
  
    //get the response from AI

    var result = await chatCompletionService.GetChatMessageContentAsync(
        chatHistory,
        executionSettings: settings,
        kernel: kernel);

    //print the LLM response

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"GoodFood : {result}");
    Console.ResetColor();


    // Regex pattern to match text enclosed within triple backticks

    string pattern = @"(\n\|.*\n)(\|[-| ]+\n)(\|.*\n)*";

    // Replace the matched segment with an empty string

    string textToRead = Regex.Replace(result.Content, pattern, "\n", RegexOptions.Singleline);
    using var synthesizer = new SpeechSynthesizer(speechConfig);
    var speech = await synthesizer.SpeakTextAsync(textToRead);

    //add the message from the agent to the chart history

    chatHistory.AddMessage(result.Role, result.Content ?? string.Empty);
} while (!string.IsNullOrEmpty(userInput));
```

#### Step 6: Implement GoodFoodPlugin Class

In this step, we define the core plugin that acts as the intelligent bridge between the user and the GoodFood backend system. The GoodFoodPlugin class uses kernel functions to:
- Fetch the menu based on the current time of day (breakfast, lunch, or dinner)
- Initialize a new order
- Add menu items to the current order

It also integrates with an event-sourcing pattern by persisting changes to the order and menu states via the EventStore and maintaining current views using the EventView.

Key methods include:

- `GetMenuItemAsync()`: Retrieves a time-based menu from Cosmos DB and returns it for display.
- `CreateNewOrder()`: Sets up a new order with a unique ID and default values.
- `AddingItemToCurrentOrder(...)`: Adds or updates items in the customer’s order, recalculating totals and updating the view.
- `ApplyAsync(...)`: A powerful internal function responsible for rebuilding current state views from the event stream—whether it’s creating, updating, or canceling orders.

You will add to these methods the following methods which details are provided in the actual program.cs

| Method Name                        | Purpose                                                                 |
|------------------------------------|-------------------------------------------------------------------------|
| `RemoveItemFromCurrentOrder`       | Removes a specified item and quantity from the current order.           |
| `GetRecapCurrentOrder`             | Retrieves and summarizes the current order, listing all items and totals. |
| `CancelCurrentOrder`               | Cancels the current order after customer confirmation.                  |
| `AddCustomerNameToCurrentOrder`    | Adds or updates the customer’s name for the current order.              |
| `ClearScreen`                      | Clears the console and prepares the interface for the next customer.    |
| `SeedingMenu` *(private)*          | Seeds the menu with default breakfast, lunch, and dinner items if none exist. |

Each method is decorated with [KernelFunction] attributes to expose them for AI-driven orchestration, allowing the Semantic Kernel to intelligently trigger the right function based on user intent.

```csharp
public class GoodFoodPlugin
{
    private readonly EventStore _estore;
    private readonly EventView _eviewstore;
    public GoodFoodPlugin()
    {
        _estore = new EventStore();
        _eviewstore = new EventView();
        SeedingMenu().Wait();
    }

    [KernelFunction("get_menu")]
    [Description("Retrieve and display the current menu with neatly formatted columns (Menu Item ID, Name, and Price) for easy readability.")]
    public async Task<FoodMnu> GetMenuItemAsync()
    {
        try
        {
            var currentTime = DateTime.Now.TimeOfDay;
            string menuTime = currentTime switch
            {
                _ when currentTime >= new TimeSpan(4, 0, 0) && currentTime < new TimeSpan(11, 0, 0) => "breakfast",
                _ when currentTime >= new TimeSpan(11, 0, 0) && currentTime < new TimeSpan(15, 0, 0) => "lunch",
                _ => "dinner"
            };
            string query = "SELECT * FROM c WHERE c.data.MenuId = @menuid";
            var parameters = new Dictionary<string, object> { { "@menuid", menuTime } };
            var foodmenu = await _eviewstore.QueryItemAsync<View<FoodMnu>>(query, parameters);
            if (foodmenu != null)
            {
                return foodmenu.data;
            }
            return null;
        }
        catch (CosmosException ex)
        {
            Console.WriteLine($"CosmosDB Error: {ex.Message}");
            return null;
        }
    }

    [KernelFunction("CreateNewOrder")]
    [Description("Initialize a new order for the customer, allowing them to add items of their choice.")]
    public async Task<string> CreateNewOrder()
    {
        try
        {
            var oid = Guid.NewGuid().ToString();
            var newOrder = new Order
            {
                orderid = oid,
                orderdate = DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss"),
                itemsnumber = 0,
                total = 0,
                customernickname = "Anonymous",
                isCanceled = false,
                orderdetails = new List<OrderDetail>(),
            };
            var res = await _estore.AppendToStreamAsync<Order>(
                newOrder.orderid,
                nEventType.NewOrderCreated.ToString(),
                typeof(Order).Name,
                newOrder
            );
            await ApplyAsync(res);
            return $"A new order has been initiated for the current customer.  Order ID: {newOrder.orderid}";
        }
        catch (Exception ex)
        {
            return "An unexpected error occurred while initiating this order.";
        }

    }

    [KernelFunction("AddItemToCurrentOrder")]
    [Description("Add a specified menu item to the current order.")]
    public async Task<string> AddingItemToCurrentOrder(int itemId,int quantity, string currentOrderId)
    {
        try
        {
            var menu = await GetMenuItemAsync();
            if (menu == null || menu.List == null || !menu.List.Any())
            {
                return "No menu items are currently available to order.";
            }

            var availableMenuItems = menu.List.ToDictionary(item => item.MenuItemId, item => item.Price);
            var availableMenuItemsName = menu.List.ToDictionary(item => item.MenuItemId, item => item.Name);
            if (!availableMenuItems.ContainsKey(itemId))
            {
                return $"Menu item ID {itemId} is not available on the current menu.";
            }
            decimal price = availableMenuItems[itemId];
            decimal subtotal = price * quantity;
            var newItem = new OrderDetail
            {
                orderdetailid = Guid.NewGuid().ToString(),
                menuitemid = itemId,
                quantity = quantity,
                unitprice = price,
                subtotal = subtotal
            };
            var res = await _estore.AppendToStreamAsync<OrderDetail>(
                currentOrderId,
                nEventType.AddNewItemAddedToOrder.ToString(),
                typeof(Order).Name,
                newItem
            );
            await ApplyAsync(res);
            return $"{quantity} {availableMenuItemsName[itemId]} added to the current customer Order ID: {currentOrderId}";
        }
        catch (Exception ex)
        {
            return "An unexpected error occurred while initiating this order.";
        }

    }
    
    private async Task ApplyAsync(dynamic @event)
    {
        var streamId = @event.streamid?.ToString();
        if (string.IsNullOrWhiteSpace(streamId))
        {
            throw new ArgumentException("Stream ID cannot be null or empty.", nameof(@event.streamid));
        }
        var e = JsonConvert.DeserializeObject<Event<dynamic>>(@event.ToString());
        if (e.entitytype == "FoodMnu")
        {
            var mnu_payload = new FoodMnu();
            var existing_mnu_event = await _eviewstore.LoadViewAsync<View<FoodMnu>>(streamId);
            dynamic mnu_classObj = existing_mnu_event.Item1;
            string _etag = existing_mnu_event.Item2;
            FoodMnu exsisting_menu = new FoodMnu();
            if (mnu_classObj.streamid != null)
            {
                exsisting_menu = JsonConvert.DeserializeObject<FoodMnu>(mnu_classObj.data.ToString());
                mnu_payload.MenuId = exsisting_menu.MenuId;
                mnu_payload.StartingTime = exsisting_menu.StartingTime;
                mnu_payload.EndTime = exsisting_menu.EndTime;
                mnu_payload.List = exsisting_menu.List;
            }
            else
            {
                mnu_payload.MenuId = e.data.MenuId;
                mnu_payload.StartingTime = e.data.StartingTime;
                mnu_payload.EndTime = e.data.EndTime;
                mnu_payload.List = e.data.List?.ToObject<List<MnuItem>>() ?? new List<MnuItem>();
            }

            _eviewstore.SaveViewAsync(streamId,
            new View<FoodMnu>
            {
                streamid = e.streamid,
                entitytype = e.entitytype,
                data = mnu_payload,
                timestamp = e.timestamp,
                version = e.version
            },
            _etag);
        }
        else
        {
            var playload = new Order();

            var r = await _eviewstore.LoadViewAsync<View<Order>>(streamId);
            dynamic classObj = r.Item1;
            string etag = r.Item2;
            Order v = new Order();
            if (classObj.streamid != null)
            {
                v = JsonConvert.DeserializeObject<Order>(classObj.data.ToString());
                playload.orderdetails = new List<OrderDetail>();
                playload.orderdetails = v.orderdetails;
                playload.customernickname = v.customernickname;
                playload.itemsnumber = v.itemsnumber;
                playload.orderid = v.orderid;
                playload.orderdate = DateTime.Now.ToString();
            }

            switch ((nEventType)Enum.Parse(typeof(nEventType), e.eventtype.ToString()))
            {
                case nEventType.NewOrderCreated:
                    playload.orderdetails = new List<OrderDetail>();
                    playload.itemsnumber = e.data.itemsnumber;
                    playload.orderid = e.data.orderid;
                    playload.customernickname = e.data.customernickname;
                    playload.orderdate = DateTime.Now.ToString();
                    break;

                case nEventType.AddNewItemAddedToOrder:
                    playload.orderid = v.orderid;
                    playload.orderdate = DateTime.Now.ToString();
                    playload.orderdetails = playload.orderdetails ?? new List<OrderDetail>();
                    var o = JsonConvert.DeserializeObject<OrderDetail>(e.data.ToString());
                    var existingItem = playload.orderdetails.FirstOrDefault(d => d.menuitemid == o.menuitemid);
                    if (existingItem != null)
                    {
                        // Update quantity
                        existingItem.quantity += o.quantity;
                        // Recalculate subtotal for this item
                        existingItem.subtotal = existingItem.quantity * existingItem.unitprice;
                    }
                    else
                    {
                        playload.orderdetails.Add(o);
                    }
                    // Recalculate total order values
                    playload.total = playload.orderdetails.Sum(d => d.subtotal);
                    playload.itemsnumber = playload.orderdetails.Sum(d => d.quantity);
                    break;

                case nEventType.ItemRemovedfromOrder:
                    playload.orderid = v.orderid;
                    playload.orderdetails = playload.orderdetails ?? new List<OrderDetail>();
                    var or = JsonConvert.DeserializeObject<OrderDetail>(e.data.ToString());
                    var existingItemRm = playload.orderdetails.FirstOrDefault(p => p.menuitemid == or.menuitemid);
                    if (existingItemRm != null)
                    {
                        if (or.quantity >= existingItemRm.quantity)
                        {
                            // Remove the item completely
                            playload.orderdetails.Remove(existingItemRm);
                        }
                        else
                        {
                            // Decrease quantity
                            existingItemRm.quantity -= or.quantity;
                            // Recalculate subtotal for this item
                            existingItemRm.subtotal = existingItemRm.quantity * existingItemRm.unitprice;
                        }
                    }

                    playload.total = playload.orderdetails.Sum(d => d.subtotal);
                    playload.itemsnumber = playload.orderdetails.Sum(d => d.quantity);
                    playload.orderdate = DateTime.Now.ToString();
                    // Update order summary
                    break;

                case nEventType.OrderCanceled:
                    playload.orderdetails = playload.orderdetails ?? new List<OrderDetail>();
                    playload.itemsnumber = v.itemsnumber;
                    playload.orderid = v.orderid;
                    playload.total = v.total;
                    playload.customernickname = v.customernickname;
                    playload.orderdate = DateTime.Now.ToString();
                    playload.isCanceled = true;
                    break;

                case nEventType.UpdateCustomerNameOnCurrentOrder:
                    var o_withname = JsonConvert.DeserializeObject<Order>(e.data.ToString());
                    playload.orderdetails = playload.orderdetails ?? new List<OrderDetail>();
                    playload.itemsnumber = v.itemsnumber;
                    playload.customernickname = o_withname.customernickname;
                    playload.orderid = v.orderid;
                    playload.orderdate = DateTime.Now.ToString();
                    playload.isCanceled = false;
                    break;
            }
            _eviewstore.SaveViewAsync(streamId,
                new View<Order>
                {
                    streamid = e.streamid,
                    entitytype = e.entitytype,
                    data = playload,
                    timestamp = e.timestamp,
                    version = e.version
                },
                etag
            );
        }
    }
}
```

#### Step 7: Data Model for the GoodFoodPlugin

The GoodFoodPlugin uses a set of data models to manage menus, orders, and order details.

- **FoodMnu**: This class represents a menu and contains properties for `MenuId`, `StartingTime`, `EndTime`, and a list of `MnuItem` objects, which are the individual menu items available during the specified time range.
- **MnuItem**: Each menu item is defined by this class, which includes properties like `MenuItemId`, `Name`, `Description`, and `Price`. These attributes describe the details of the dish offered in the menu.
- **Order**: This class holds the details of an order placed by a customer. It includes the `orderid` (unique identifier), `orderdate` (when the order was placed), `itemsnumber` (the number of items in the order), `total` (total price of the order), `customernickname` (a reference to the customer), and a list of `OrderDetail` objects. Additionally, it has an `isCanceled` property to indicate if the order was canceled.
- **OrderDetail**: This class represents the details of each item within an order. It contains `orderdetailid` (unique identifier for the order item), `menuitemid` (ID of the menu item), `quantity` (the number of units ordered), `unitprice` (the price of one unit), and `subtotal` (the total cost for that specific item in the order).

Each of these classes is linked together to create a structured flow of data, representing menus, items, customer orders, and their details, allowing for an efficient management of food orders in the plugin system.


```csharp
public class FoodMnu
{
    [JsonPropertyName("menuid")]
    public string MenuId { get; set; }
    [JsonPropertyName("startingtime")]
    public string StartingTime { get; set; }
    [JsonPropertyName("endtime")]
    public string EndTime { get; set; }
    [JsonPropertyName("list")]
    public List<MnuItem> List { get; set; }
}
public class MnuItem
{
    [JsonPropertyName("MenuItemId")]
    public int MenuItemId { get; set; }
    [JsonPropertyName("Name")]
    public string Name { get; set; }
    [JsonPropertyName("Description")]
    public string Description { get; set; }
    [JsonPropertyName("Price")]
    public decimal Price { get; set; }
}

public class Order
{
    [JsonPropertyName("orderid")]
    public string orderid { get; set; }
    [JsonPropertyName("orderdate")]
    public string orderdate { get; set; }
    [JsonPropertyName("itemsnumber")]
    public int itemsnumber { get; set; }
    [JsonPropertyName("total")]
    public decimal total { get; set; }
    [JsonPropertyName("customernickname")]
    public string customernickname { get; set; }
    [JsonPropertyName("orderdetails")]
    public List<OrderDetail> orderdetails { get; set; }
    [JsonPropertyName("iscanceled")]
    public bool isCanceled { get; set; }
}
public class OrderDetail
{
    [JsonPropertyName("orderdetailid")]
    public string orderdetailid { get; set; }
    [JsonPropertyName("menuitemid")]
    public int menuitemid { get; set; }
    [JsonPropertyName("quantity")]
    public int quantity { get; set; }
    [JsonPropertyName("unitprice")]
    public decimal unitprice { get; set; }
    [JsonPropertyName("subtotal")]
    public decimal subtotal { get; set; }
}
```

#### Step 8: Basic Event Sourcing for drivethru operation

In this step, event sourcing is implemented to handle the state transitions for various actions in the drivethru operation, such as creating orders, adding/removing items, and processing payments.

#### **1. Event Types (nEventType Enum)**

The `nEventType` enum defines the different types of events that can occur in the system:

- **NewOrderCreated**: When a new order is created.
- **AddNewItemAddedToOrder**: When a new item is added to an existing order.
- **ItemRemovedfromOrder**: When an item is removed from an order.
- **OrderCanceled**: When an order is canceled.
- **OrderPaymentProcessed**: When payment for an order is processed.
- **UpdateCustomerNameOnCurrentOrder**: When the customer's name is updated on an order.
- **NewMenuCreated**: When a new menu is created.

```csharp
public enum nEventType
{
    None = 0,
    NewOrderCreated = 1,
    AddNewItemAddedToOrder = 2,
    ItemRemovedfromOrder = 3,
    OrderCanceled = 4,
    OrderPaymentProcessed = 6,
    UpdateCustomerNameOnCurrentOrder = 7,
    NewMenuCreated = 8,
}
```

#### **2. Event Class**

The `Event<T>` class represents a specific event. It includes the following properties:

- `id`: A unique identifier for the event.
- `streamid`: The identifier for the event stream (this links events to specific entities).
- `version`: The version of the event.
- `entitytype`: The type of entity the event is related to (e.g., order, menu).
- `eventtype`: The type of event (e.g., `NewOrderCreated`).
- `data`: The actual event data, which is a generic type `T`.
- `timestamp`: The date and time when the event occurred.

```csharp
public class Event<T>
{
    public string id { get; set; }
    public string streamid { get; set; }
    public int version { get; set; }
    public string entitytype { get; set; }
    public string eventtype { get; set; }
    public T data { get; set; }
    public DateTime timestamp { get; set; }
}
```

#### **3. EventStream Class**

The `EventStream<T>` class represents a sequence of events for a specific entity. It contains:

- `Id`: The identifier for the stream.
- `Version`: The version of the event stream.
- `Events`: A collection of events associated with the stream.

This class is used to store and manage a series of events.
```csharp
public class EventStream<T>
{
    private readonly List<Event<T>> _events;
    public EventStream(string id, int version, IEnumerable<Event<T>> events)
    {
        Id = id;
        Version = version;
        _events = events.ToList();
    }

    public string Id { get; private set; }
    public int Version { get; private set; }
    public IEnumerable<Event<T>> Events
    {
        get { return _events; }
    }
}
```

#### **4. EventStore Class**

The `EventStore` class interacts with the Cosmos DB to store and load events. It has the following methods:

- **AppendToStreamAsync**: Appends an event to the event stream. It generates a new `Event<T>` object and saves it to Cosmos DB.
- **LoadStreamAsync**: Loads the events for a specific stream by querying the Cosmos DB for the events and returning them in the form of an `EventStream<T>`.

This class allows for the persistence of events and enables event sourcing, ensuring the state transitions of entities are properly tracked over time.
```csharp
public class EventStore
{
    private readonly CosmosClient _cl;
    private Microsoft.Azure.Cosmos.Container _cn;
    private Database _db;
    public EventStore()
    {
        _cl = new CosmosClient("https://localhost:8081", "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
        InitializeDatabaseAndContainer().Wait();
    }

    private async Task InitializeDatabaseAndContainer()
    {
        _db = await _cl.CreateDatabaseIfNotExistsAsync("goodfooddb");
        _cn = _db.CreateContainerIfNotExistsAsync("events", "/streamid").Result;
    }

    public async Task<dynamic> AppendToStreamAsync<T>(string streamId, string eventType, string entityType, T eventData)
    {
        try
        {
            var eventItem = new Event<T>
            {
                id = Guid.NewGuid().ToString(),
                streamid = streamId,
                eventtype = eventType,
                entitytype = typeof(T).Name,
                data = eventData,
                timestamp = DateTime.UtcNow
            };

            var response = await _cn.Scripts.ExecuteStoredProcedureAsync<dynamic>(
                "SpAppendToStream",
                new PartitionKey(streamId),
                new object[] { streamId, eventItem }
            );

            // Ensure response is a JSON object before returning it
            if (response.Resource is string responseString)
            {
                throw new Exception($"Stored procedure returned an error: {responseString}");
            }

            return response.Resource;
        }
        catch (Exception ex)
        {
            return ex.Message.ToString();
        }
    }
    public async Task<EventStream<T>> LoadStreamAsync<T>(string streamId)
    {

        var sqlQueryText = "SELECT * FROM events e"
            + " WHERE e.stream.id = @streamId"
            + " ORDER BY e.stream.version";

        QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText)
            .WithParameter("@streamId", streamId);

        int version = 0;
        var events = new List<Event<T>>();

        FeedIterator<Event<T>> feedIterator = _cn.GetItemQueryIterator<Event<T>>(queryDefinition);
        while (feedIterator.HasMoreResults)
        {
            FeedResponse<Event<T>> response = await feedIterator.ReadNextAsync();
            foreach (var eventWrapper in response)
            {
                version = eventWrapper.version;
                events.Add(eventWrapper);
            }
        }

        return new EventStream<T>(streamId, version, events);
    }
}
```

#### **5. View Class**

The `View<T>` class represents a projection of an event stream. It stores:

- `id`: The unique identifier for the view.
- `streamid`: The identifier for the event stream.
- `version`: The version of the view.
- `data`: The data associated with the view.
- `entitytype`: The type of entity the view is for (e.g., order).
- `timestamp`: The timestamp of when the view was created.

```csharp
public class View<T>
{
    public string id { get; set; }
    public string streamid { get; set; }
    public int version { get; set; }
    public T data { get; set; }
    public string entitytype { get; set; }
    public DateTime timestamp { get; set; }
}
```

#### **6. EventView Class**

The `EventView` class is used to manage event views and interacts with Cosmos DB. It includes the following methods:

- **SaveViewAsync**: Saves or updates a view in Cosmos DB, handling optimistic concurrency using ETag values.
- **LoadViewAsync**: Loads a view from Cosmos DB based on the `streamid`.
- **QueryItemAsync**: Executes a query on the Cosmos DB container and returns a specific item matching the query.
- **ForceDropEventViewAsync**: Deletes an event view from Cosmos DB by its `streamId` and `eventId`.

This class enables efficient handling and querying of event projections.

```csharp
public class EventView
{
    private readonly CosmosClient _cl;
    private Microsoft.Azure.Cosmos.Container _cn;
    private Database _db;
    public EventView()
    {

        _cl = new CosmosClient("https://localhost:8081", "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
        InitializeDatabaseAndContainer().Wait();
    }

    private async Task InitializeDatabaseAndContainer()
    {
        _db = await _cl.CreateDatabaseIfNotExistsAsync("goodfooddb");
        _cn = _db.CreateContainerIfNotExistsAsync("views", "/streamid").Result;
    }

    public async Task<dynamic> SaveViewAsync<T>(string streamId, View<T> view, string? etag)
    {
        var partitionKey = new PartitionKey(streamId);

        var item = new View<T>
        {
            id = streamId,
            streamid = streamId,
            entitytype = typeof(T).Name.ToString(),
            version = view.version,
            timestamp = view.timestamp,
            data = view.data,
        };

        try
        {
            if (etag != null)
            {
                var response = await _cn.UpsertItemAsync<View<T>>(item, partitionKey, new ItemRequestOptions
                {
                    IfMatchEtag = etag
                });
                return response.Resource;
            }
            else
            {
                var response = await _cn.UpsertItemAsync<View<T>>(item, partitionKey);
                return response.Resource;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            return null;
        }
    }

    public async Task<(dynamic Resource, string? ETag)> LoadViewAsync<T>(string streamid)
    {
        var partitionKey = new PartitionKey(streamid);
        try
        {
            var response = await _cn.ReadItemAsync<dynamic>(streamid, partitionKey);
            return (response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return (new View<T>(), null);
        }
    }
    public async Task<T?> QueryItemAsync<T>(string query, Dictionary<string, object>? parameters = null)
    {
        var queryDefinition = new QueryDefinition(query);

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                queryDefinition = queryDefinition.WithParameter(param.Key, param.Value);
            }
        }
        var iterator = _cn.GetItemQueryIterator<T>(queryDefinition);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return default;
    }
    public async Task<bool> ForceDropEventViewAsync<T>(string streamId, string eventId)
    {
        try
        {
            await _cn.DeleteItemAsync<T>(eventId, new PartitionKey(streamId));
            return true;
        }
        catch (Exception ex) { return false; }
    }
}
```

This event sourcing setup provides an efficient way to capture and manage state transitions in a drivethru operation, supporting the management of orders and related actions in a consistent and scalable manner.

### **How It Works (Flow)**
1. Load credentials from `appsettings.json`.
2. Build AI kernel and add `GoodFoodPlugin`.
3. Set up chat assistant with a predefined system message.
4. Continuously read user input:
   - Query AI assistant for a response.
   - Speak the response aloud.
   - Store chat history.
5. Process orders by interacting with CosmosDB.
6. Repeat as long as there is a customer.

---

### **Potential Enhancements**
- **Improve Speech Output**: Support multiple voices and languages.
- **Improve UI**: Convert to a web-based chatbot using Blazor.



