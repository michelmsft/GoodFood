# Fast Food Drive thru Operator Ai Agent with Semantic Kernel and GPT 4o mini

### **Fast Food Drive-Thru Operator AI Agent**  

This AI-powered **Fast Food Drive-Thru Operator** leverages **Semantic Kernel** and **Azure OpenAI GPT-3.5 turbo** to streamline customer interactions, improve efficiency, and automate order processing.  

### **How It Works**  
1. **Speech & AI Interaction** – The system uses **Azure OpenAI GPT-4o mini** with **Semantic Kernel Planner** for intelligent conversation and order handling.  
2. **Database Management** – Order details are stored and retrieved using **Cosmos DB for NoSQL**.  
3. **Voice Responses** – A **.NET Console App** integrates **System Speech Synthesis** to communicate with customers.  
4. **Custom Plugins** – C# plugins handle database CRUD operations for seamless transactions.

![image](https://github.com/user-attachments/assets/7105ba04-0685-48e0-975c-8311662353a8)


This AI agent enhances the **drive-thru experience** with fast, intelligent, and voice-driven automation! 

## Phase 1: Backend Database for Fast Food ops

You can use Azure CLI command sequence to create an Azure Cosmos DB account, a database named `goodfooddb`, and two containers (`events` and `views`) with their respective partition keys.

### Step 1: Set Variables
```sh
RESOURCE_GROUP="your-resource-group"
COSMOS_DB_ACCOUNT="your-cosmosdb-account-name"
DB_NAME="goodfooddb"
LOCATION="eastus"  # Change as needed
```

### Step 2: Create Azure Cosmos DB Account
```sh
az cosmosdb create \
    --name $COSMOS_DB_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --locations regionName=$LOCATION \
    --default-consistency-level Strong \
    --kind GlobalDocumentDB
```

### Step 3: Create the Database
```sh
az cosmosdb sql database create \
    --account-name $COSMOS_DB_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --name $DB_NAME
```
### Step 4: Create the `events` Container
 ```sh
 az cosmosdb sql container create \
     --account-name $COSMOS_DB_ACCOUNT \
     --resource-group $RESOURCE_GROUP \
     --database-name $DB_NAME \
     --name "events" \
     --partition-key-path "/streamid" \
     --throughput 400
 ```
 
 ### Step 5: Create the `views` Container
 ```sh
 az cosmosdb sql container create \
     --account-name $COSMOS_DB_ACCOUNT \
     --resource-group $RESOURCE_GROUP \
     --database-name $DB_NAME \
     --name "views" \
     --partition-key-path "/streamid" \
     --throughput 400
 ```

### Step 6: Create a stored procedure called `SpAppendToStream` in the `events` container
#### 6.1 Create the JavaScript File
Save the following code in a file named spAppendToStream.js:
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
#### 6.2 Deploy the Stored Procedure Using Azure CLI
Run the following command in the terminal (ensure you have az CLI installed and logged in):
```sh
az cosmosdb sql stored-procedure create \
    --account-name $COSMOS_DB_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --database-name $DB_NAME \
    --container-name "events" \
    --name "SpAppendToStream" \
    --body "$(cat spAppendToStream.js)"

```

## Phase 2: FrontEnd Fast Food ops using a .Net Console app 

### Step-by-Step Guide to Implementing GoodFood Virtual Drive-Thru Assistant

#### Prerequisites

1. **Install Required Packages**:
   - Microsoft.SemanticKernel
   - Microsoft.SemanticKernel.ChatCompletion
   - Microsoft.Azure.Cosmos
   - System.ComponentModel
   - Microsoft.SemanticKernel.Connectors.AzureOpenAI
   - Azure
   - System.Text.Json.Serialization
   - System.Speech.Synthesis
   - System.Text.RegularExpressions
   - Microsoft.Extensions.Configuration
   - Microsoft.Extensions.Caching.Memory
   - System.Net
   - Newtonsoft.Json
   - System.IO
   - System.Collections.Generic
   - System.Security.Cryptography
   - Microsoft.Azure.Cosmos.Serialization.HybridRow

#### Step 1: Load Credential Data from `appsettings.json`

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

string? cosmosdbUrl = config["CosmosDbSettings:CosmosDbUrl"];
string? cosmosdbKey = config["CosmosDbSettings:CosmosDbKey"];

if (string.IsNullOrEmpty(apiEndPointUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiModelName))
{
    Console.WriteLine("Please check your appsettings.json file for missing or incorrect values.");
    return;
}

```
#### Step 2: Build the Kernel
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
```csharp
// Add a plugin 
kernel.Plugins.AddFromType<GoodFoodPlugin>("DriveThru");
```
#### Step 4: Enable Planning
```csharp


AzureOpenAIPromptExecutionSettings settings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    Temperature = 0.3
};


```
#### Step 5: Instantiate Messaging and Chat
```csharp


// Create Chat History and add our system message
var chatHistory = new ChatHistory();
chatHistory.Add(
    new(){
        Role = AuthorRole.System,
        Content = @"You are a virtual drive-thru assistant at GoodFood, helping customers with their orders by providing menu details, 
        managing their selections, and guiding them through the ordering process. Be polite and brief in your response.

        - Always begin by using this exact message 'Welcome to GoodFood! How can I help you today ?' to welcome the new customer. Then proceed to handle the order.
        - When handling orders from new customer, you will initialize a new order session before the customer adds items.
        - Always display the current menu with neatly formatted columns (Menu Item ID, Name (30 characters only), and Price) for easy readability
        - during your conversation with customer, when they select a menu item, add it to their order using AddItemToCurrentOrder or if they want to remove an item, confirm and update the order using AddItemFromCurrentOrder.
        - If the customer asks, provide a summary of their current order using  RecapCurrentOrder. 
        - When finalizing the Order, always ask for the customer’s name. only when you have their name, you will  direct them to the next window for payment and say goodbye.
        - if for some reasons, customer request to cancel the Order, confirm with the customer before canceling their entire order using CancelCurrentOrder.
        - you will always clear the screen when the order is completed or canceled using ClearScreen and welcome a new customer.
        "
    }
);

string? userInput;
do
{
    //collect user input
    Console.Write("You : ");
    userInput = Console.ReadLine();
    if (string.IsNullOrEmpty(userInput))
    {
        continue;
    }
    //add user input
    chatHistory.AddUserMessage(userInput);

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

    // Create a new instance of the SpeechSynthesizer.
    using (SpeechSynthesizer synthesizer = new SpeechSynthesizer())
    {
        synthesizer.SetOutputToDefaultAudioDevice();
        synthesizer.SelectVoice("Microsoft David Desktop");
        synthesizer.Speak(textToRead);
    }

    //add the message from the agent to the chart history
    chatHistory.AddMessage(result.Role, result.Content ?? string.Empty);
} while (!string.IsNullOrEmpty(userInput));


```

#### Step 6: Implement GoodFoodPlugin Class
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
##### 6.1 Additional Plugins method
###### 6.1.1 RemoveItemFromCurrentOrder Method
```csharp
[KernelFunction("RemoveItemFromCurrentOrder")]
    [Description("Remove a specified item from the current order after confirming with the customer.")]
    public async Task<string> RemoveItemFromCurrentOrder(int itemId, int quantity, string currentOrderId)
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
                nEventType.ItemRemovedfromOrder.ToString(),
                typeof(Order).Name,
                newItem
            );
            await ApplyAsync(res);

            return $"{quantity} {availableMenuItemsName[itemId]} removed to the current customer Order ID: {currentOrderId}";
        }

        catch (Exception ex)
        {
            return "An unexpected error occurred while initiating this order.";
        }

    }
```
###### 6.1.2 RecapCurrentOrder Method
```csharp
    [KernelFunction("RecapCurrentOrder")]
    [Description("Provide a summary of the current order, listing selected items and their total cost.")]
    public async Task<dynamic> GetRecapCurrentOrder(string currentOrderId)
    {
        try
        {
            string query = "SELECT * FROM c WHERE c.streamid = @streamid";
            var parameters = new Dictionary<string, object> { { "@streamid", currentOrderId } };

            var cOrder = await _eviewstore.QueryItemAsync<View<Order>>(query, parameters);
            if (cOrder != null) {
                return cOrder.data;
            }
            else
            {
                return null;
            }
           
        }

        catch (Exception ex)
        {
            return $"An unexpected error occurred while retrieving the current order id {currentOrderId}.";
        }

    }
```
###### 6.1.3 CancelCurrentOrder Method
```csharp
    [KernelFunction("CancelCurrentOrder")]
    [Description("Cancel the entire order after confirming with the customer.")]
    public async Task<string> CancelCurrentOrder(string currentOrderId)
    {
        try
        {

            var res = await _estore.AppendToStreamAsync<dynamic>(
                currentOrderId,
                nEventType.OrderCanceled.ToString(),
                typeof(Order).Name,
                null
            );
            await ApplyAsync(res);

            return $"Your order # {currentOrderId} has been canceled successfully.";

        }

        catch (Exception ex)
        {
            return $"An unexpected error occurred while canceling the current order id {currentOrderId}.";
        }

    }
```
###### 6.1.4 AddCustomerNameToCurrentOrder Method
```csharp
    [KernelFunction("AddCustomerNameToCurrentOrder")]
    [Description("Add Customer name to the current order.")]
    public async Task<string> AddCustomerNameToCurrentOrder(string currentOrderId, string customerName)
    {
        try
        {
            var OrderWithCustomerName = new Order
            {
                orderid = currentOrderId,
                orderdate = DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss"),
                itemsnumber = 0,
                total = 0,
                customernickname = customerName,
                isCanceled = false,
                orderdetails = new List<OrderDetail>(),
            };

            var res = await _estore.AppendToStreamAsync<dynamic>(
                currentOrderId,
                nEventType.UpdateCustomerNameOnCurrentOrder.ToString(),
                typeof(Order).Name,
                OrderWithCustomerName
            );
            await ApplyAsync(res);

            return $"The name on the order # {currentOrderId} has been updated successfully.";

        }

        catch (Exception ex)
        {
            return $"An unexpected error occurred while canceling the current order id {currentOrderId}.";
        }

    }
```
###### 6.1.5 ClearScreen Method
```csharp
    [KernelFunction("ClearScreen")]
    [Description("Clears the console screen and welcome the next customer.")]
    public void ClearScreen()
    {
        Thread.Sleep(2000);
        Console.Clear();
    }
```
###### 6.1.6 Internal private SeedingMenu Method
```csharp
    private async Task SeedingMenu()
    {
        string query = "SELECT * FROM c WHERE c.entitytype = @entity";
        var parameters = new Dictionary<string, object> { { "@entity", "FoodMnu" } };

        var mnu = await _eviewstore.QueryItemAsync<View<FoodMnu>>(query, parameters);
        if (mnu == null)
        {

            var breakfastMenu = new FoodMnu
            {
                MenuId = "breakfast",
                StartingTime = "04:00:00 AM",
                EndTime = "10:59:59 AM",
                List = new List<MnuItem>
                {
                    new MnuItem { MenuItemId = 1, Name = "Pancakes with Syrup", Description = "Fluffy pancakes with syrup", Price = 5.99m },
                    new MnuItem { MenuItemId = 2, Name = "Scrambled Eggs with Toast", Description = "Scrambled eggs with toast", Price = 4.99m },
                    new MnuItem { MenuItemId = 3, Name = "Bacon and Egg Sandwich", Description = "Bacon and egg sandwich", Price = 6.99m },
                    new MnuItem { MenuItemId = 4, Name = "French Toast", Description = "French toast with syrup", Price = 5.99m },
                    new MnuItem { MenuItemId = 5, Name = "Breakfast Burrito", Description = "Burrito with eggs, bacon, and cheese", Price = 7.99m },
                    new MnuItem { MenuItemId = 6, Name = "Oatmeal with Fruit", Description = "Oatmeal topped with fresh fruit", Price = 4.99m },
                    new MnuItem { MenuItemId = 7, Name = "Sausage and Egg Muffin", Description = "Muffin with sausage and egg", Price = 5.99m },
                    new MnuItem { MenuItemId = 8, Name = "Yogurt Parfait", Description = "Yogurt with granola and fruit", Price = 3.99m },
                    new MnuItem { MenuItemId = 9, Name = "Bagel with Cream Cheese", Description = "Bagel with cream cheese", Price = 3.99m },
                    new MnuItem { MenuItemId = 10, Name = "Waffles with Berries", Description = "Waffles topped with berries", Price = 6.99m }
                }
            };

            var breakfast_res = await _estore.AppendToStreamAsync<FoodMnu>(
                Guid.NewGuid().ToString(),
                nEventType.NewMenuCreated.ToString(),
                "FoodMnu",
                breakfastMenu
            );
            await ApplyAsync(breakfast_res);


            var lunchMenu = new FoodMnu
            {
                MenuId = "lunch",
                StartingTime = "11:00:00 AM",
                EndTime = "03:59:59 PM",
                List = new List<MnuItem>
            {
                new MnuItem { MenuItemId = 11, Name = "Cheeseburger", Description = "Juicy beef burger with cheese", Price = 8.99m },
                new MnuItem { MenuItemId = 12, Name = "Grilled Chicken Sandwich", Description = "Grilled chicken sandwich with lettuce and tomato", Price = 7.99m },
                new MnuItem { MenuItemId = 13, Name = "Caesar Salad", Description = "Caesar salad with croutons and parmesan", Price = 6.99m },
                new MnuItem { MenuItemId = 14, Name = "Turkey Club Sandwich", Description = "Turkey club sandwich with bacon and avocado", Price = 9.99m },
                new MnuItem { MenuItemId = 15, Name = "Veggie Wrap", Description = "Wrap with assorted vegetables and hummus", Price = 7.99m },
                new MnuItem { MenuItemId = 16, Name = "Chicken Caesar Wrap", Description = "Wrap with chicken, lettuce, and Caesar dressing", Price = 8.99m },
                new MnuItem { MenuItemId = 17, Name = "BLT Sandwich", Description = "Bacon, lettuce, and tomato sandwich", Price = 6.99m },
                new MnuItem { MenuItemId = 18, Name = "Tuna Salad Sandwich", Description = "Tuna salad sandwich with lettuce", Price = 7.99m },
                new MnuItem { MenuItemId = 19, Name = "BBQ Pulled Pork Sandwich", Description = "Pulled pork sandwich with BBQ sauce", Price = 9.99m },
                new MnuItem { MenuItemId = 20, Name = "Chicken Quesadilla", Description = "Quesadilla with chicken and cheese", Price = 8.99m }
            }
            };

            var lunch_res = await _estore.AppendToStreamAsync<FoodMnu>(
                Guid.NewGuid().ToString(),
                nEventType.NewMenuCreated.ToString(),
                "FoodMnu",
                lunchMenu
            );
            await ApplyAsync(lunch_res);


            var dinnerMenu = new FoodMnu
            {
                MenuId = "dinner",
                StartingTime = "04:00:00 PM",
                EndTime = "01:59:59 AM",
                List = new List<MnuItem>
            {
                new MnuItem { MenuItemId = 21, Name = "Grilled Steak with Vegetables", Description = "Grilled steak with a side of vegetables", Price = 15.99m },
                new MnuItem { MenuItemId = 22, Name = "Spaghetti Bolognese", Description = "Spaghetti with Bolognese sauce", Price = 12.99m },
                new MnuItem { MenuItemId = 23, Name = "Grilled Salmon with Rice", Description = "Grilled salmon with a side of rice", Price = 14.99m },
                new MnuItem { MenuItemId = 24, Name = "Chicken Alfredo Pasta", Description = "Pasta with Alfredo sauce and chicken", Price = 13.99m },
                new MnuItem { MenuItemId = 25, Name = "Beef Tacos", Description = "Tacos with seasoned beef and toppings", Price = 11.99m },
                new MnuItem { MenuItemId = 26, Name = "Shrimp Scampi", Description = "Shrimp scampi with garlic butter sauce", Price = 16.99m },
                new MnuItem { MenuItemId = 27, Name = "BBQ Ribs", Description = "BBQ ribs with a side of coleslaw", Price = 17.99m },
                new MnuItem { MenuItemId = 28, Name = "Chicken Parmesan", Description = "Chicken Parmesan with marinara sauce", Price = 14.99m },
                new MnuItem { MenuItemId = 29, Name = "Beef Stir Fry", Description = "Beef stir fry with vegetables", Price = 13.99m },
                new MnuItem { MenuItemId = 30, Name = "Vegetable Lasagna", Description = "Lasagna with assorted vegetables", Price = 12.99m }
            }
            };

            var dinner_res = await _estore.AppendToStreamAsync<FoodMnu>(
                Guid.NewGuid().ToString(),
                nEventType.NewMenuCreated.ToString(),
                "FoodMnu",
                dinnerMenu
            );
            await ApplyAsync(dinner_res);

        }
    }

```
#### Step 7: Data Model for the GoodFoodPlugin
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
```csharp
public enum nEventType
{
    None = 0,

    #region Order Management

    NewOrderCreated = 1,
    AddNewItemAddedToOrder = 2,
    ItemRemovedfromOrder = 3,
    OrderCanceled = 4,
    OrderPaymentProcessed = 6,
    UpdateCustomerNameOnCurrentOrder = 7,

    #endregion

    #region Menu Management

    NewMenuCreated = 8,
    #endregion



}

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

public class View<T>
{
    public string id { get; set; }
    public string streamid { get; set; }
    public int version { get; set; }
    public T data { get; set; }
    public string entitytype { get; set; }
    public DateTime timestamp { get; set; }

}
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
- **Enhance Order Management**: Allow updates and cancellations.
- **Improve UI**: Convert to a web-based chatbot using Blazor.



