﻿#nullable disable
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
using Azure.Identity;
using Azure.Core;
using System.Runtime.InteropServices;


#region Load credential data from appsettings.json

// Get the root directory of the application

string rootPath = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName
                    ?? AppContext.BaseDirectory;

// Load configuration from appsettings.json in the root folder

var config = new ConfigurationBuilder()
    .SetBasePath(rootPath) // Set the base path to the root directory
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();


// Retrieve values from the configuration

string apiKey = config["ApiSettings:ApiKey"];
string apiEndPointUrl = config["ApiSettings:ApiEndPointUrl"];
string apiModelName = config["ApiSettings:ApiModelName"];


string SpeechApiKey = config["ApiSettings:SpeechServiceKey"];
string SpeechApiEndPointUrl = config["ApiSettings:SpeechServiceEndPoint"];
string SpeechApiRegion = config["ApiSettings:SpeechServiceRegion"];
string speechResourceId = config["ApiSettings:SpeechResourceID"];

string cosmosdbUrl = config["CosmosDbSettings:CosmosDbUrl"];
string cosmosdbKey = config["CosmosDbSettings:CosmosDbKey"];


// if (string.IsNullOrEmpty(apiEndPointUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiModelName))
// {
//     Console.WriteLine("Please check your appsettings.json file for missing or incorrect values.");
//     return;
// }

#endregion

#region Build the Kernel

// Create a kernel with Azure OpenAI chat completion
var credential = new DefaultAzureCredential();

IKernelBuilder builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(
    deploymentName: apiModelName,
    endpoint: apiEndPointUrl,
    credentials: credential
);


// Build the kernel

Kernel kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

#endregion


// Add a plugin 

var goodFoodPlugin = new GoodFoodPlugin(cosmosdbUrl, credential);
kernel.Plugins.AddFromObject(goodFoodPlugin, "DriveThru");


#region Enable planning

AzureOpenAIPromptExecutionSettings settings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    Temperature = 0.3
};

#endregion

#region Instantiate messaging and chat

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
        - if customer ask for a menu item that is not on the current menu, suggest them the closest one on the current menu.
        - If the customer asks, provide a summary of their current order using  RecapCurrentOrder. 
        - When finalizing the Order, always ask for the customer’s name. only when you have their name, you will  direct them to the next window for payment and say goodbye.
        - if for some reasons, customer request to cancel the Order, confirm with the customer before canceling their entire order using CancelCurrentOrder.
        - you will always clear the screen when the order is completed or canceled using ClearScreen and welcome a new customer.
        "
    }
);

int _retry = 0;
string userInput=null;

string[] scopes =  ["https://cognitiveservices.azure.com/.default"];
var tokenContainer = await credential.GetTokenAsync(new TokenRequestContext(scopes));
var token = tokenContainer.Token;
string authorizationToken = $"aad#{speechResourceId}#{token}";

SpeechConfig speechConfig = SpeechConfig.FromAuthorizationToken(authorizationToken, SpeechApiRegion);

do
{

    Console.Write($"You :");


    var recognizer = new SpeechRecognizer(speechConfig);
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
        _retry++;
        Console.Write($"\n");
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

    // using speech synthetizer to read aloud the model output
    using var synthesizer = new SpeechSynthesizer(speechConfig);
    var speech = await synthesizer.SpeakTextAsync(textToRead);


    //add the message from the agent to the chart history
    chatHistory.AddMessage(result.Role, result.Content ?? string.Empty);
} while (!string.IsNullOrEmpty(userInput));

#endregion

/****************************************************** 

The GoodFoodPlugin class is designed to manage food menu items and customer orders in a restaurant setting. 
It utilizes two main components: EventStore and EventView, to handle data storage and retrieval. 
The plugin includes several methods for interacting with the menu and orders, such as GetMenuItemAsync 
to retrieve the current menu based on the time of day, CreateNewOrder to initialize a new order, 
AddingItemToCurrentOrder and RemoveItemFromCurrentOrder to add or remove items from an order, 
GetRecapCurrentOrder to provide a summary of the current order, and CancelCurrentOrder to cancel an order. 
Additionally, it has a method AddCustomerNameToCurrentOrder to update the customer's name on an order and 
ClearScreen to clear the console screen. The SeedingMenu method is used to populate the menu with initial items if they do not already exist. 
The plugin handles errors gracefully and ensures that the operations are performed asynchronously for better performance.
 
 ********************************************************/

public class GoodFoodPlugin
{
    private readonly EventStore _estore;
    private readonly EventView _eviewstore;

    public GoodFoodPlugin(string cosmosdbUrl, string cosmosdbKey)
    {
        _estore = new EventStore(cosmosdbUrl, cosmosdbKey);
        _eviewstore = new EventView(cosmosdbUrl, cosmosdbKey);
        SeedingMenu().Wait();
    }

    public GoodFoodPlugin(string cosmosdbUrl, TokenCredential credential)
    {
        _estore = new EventStore(cosmosdbUrl, credential);
        _eviewstore = new EventView(cosmosdbUrl, credential);
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

        catch (Exception)
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

        catch (Exception)
        {
            return "An unexpected error occurred while initiating this order.";
        }

    }

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

        catch (Exception)
        {
            return "An unexpected error occurred while initiating this order.";
        }

    }

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

        catch (Exception)
        {
            return $"An unexpected error occurred while retrieving the current order id {currentOrderId}.";
        }

    }

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

        catch (Exception)
        {
            return $"An unexpected error occurred while canceling the current order id {currentOrderId}.";
        }

    }

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

        catch (Exception)
        {
            return $"An unexpected error occurred while canceling the current order id {currentOrderId}.";
        }

    }

    [KernelFunction("ClearScreen")]
    [Description("Clears the console screen and welcome the next customer.")]
    public void ClearScreen()
    {
        Thread.Sleep(2000);
        Console.Clear();
    }

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


/****************************************************** 

Data Model used in the GoodFoodPlugin
 
 ********************************************************/

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



/****************************************************** 

Event Sourcing in a fast-food app using Azure Cosmos DB for NoSQL captures and stores all state changes as a sequence of immutable events.
this is a basic implemenation using Key components such as Event Store, which persists order events (e.g., NewOrderCreated, ItemAdded, ItemRemoved, CustomerNameUpdated,etc); 
an Event Handlers AppendToStreamAsync and SaveViewAsync, which update materialized views; 
 
 ********************************************************/

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
    public EventStore(string cosmosdbUrl, string cosmosdbKey)
    {
        _cl = new CosmosClient(cosmosdbUrl, cosmosdbKey);
        InitializeDatabaseAndContainer().Wait();
    }

    public EventStore(string cosmosdbUrl, TokenCredential credential)
    {
        _cl = new CosmosClient(cosmosdbUrl, credential);
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
    public EventView(string cosmosdbUrl, string cosmosdbKey)
    {

        _cl = new CosmosClient(cosmosdbUrl, cosmosdbKey);
        InitializeDatabaseAndContainer().Wait();
    }

    public EventView(string cosmosdbUrl, TokenCredential credential)
    {
        _cl = new CosmosClient(cosmosdbUrl, credential);
        InitializeDatabaseAndContainer().Wait();
    }

    private async Task InitializeDatabaseAndContainer()
    {
        _db = await _cl.CreateDatabaseIfNotExistsAsync("goodfooddb");
        _cn = _db.CreateContainerIfNotExistsAsync("views", "/streamid").Result;
    }

    public async Task<dynamic> SaveViewAsync<T>(string streamId, View<T> view, string etag)
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

    public async Task<(dynamic Resource, string ETag)> LoadViewAsync<T>(string streamid)
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
    public async Task<T> QueryItemAsync<T>(string query, Dictionary<string, object> parameters = null)
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
        catch (Exception) { return false; }
    }
}