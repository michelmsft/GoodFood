
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Azure.Cosmos;
using System.ComponentModel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Azure;
using System.Text.Json.Serialization;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;




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

#endregion

#region Build the Kernel

// Create a kernel with Azure OpenAI chat completion
IKernelBuilder builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(
    deploymentName: apiModelName,
    endpoint: apiEndPointUrl,
    apiKey: apiKey
);


// Build the kernel
Kernel kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

#endregion


// Add a plugin 
kernel.Plugins.AddFromType<GoodFoodPlugin>("DriveThru");

#region Enable planning

AzureOpenAIPromptExecutionSettings settings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

#endregion

#region Instantiate messaging and chat

// Create Chat History and add system message

var chatHistory = new ChatHistory();
chatHistory.Add(
    new(){
        Role = AuthorRole.System,
        Content = @"You are a virtual drive-thru assistant at GoodFood, helping users with their orders by providing information on menu items, 
        taking orders, and processing payments."
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

    //print the results

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

#endregion


public class GoodFoodPlugin
{
    private readonly CosmosClient _cl;
    private Database _db;
    private Microsoft.Azure.Cosmos.Container _cn;

    public GoodFoodPlugin()
    {
        _cl = new CosmosClient("https://localhost:8081", 
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");

        InitializeDatabaseAndContainer().Wait(); // Synchronously wait for async method
    }

    private async Task InitializeDatabaseAndContainer()
    {
        _db = await _cl.CreateDatabaseIfNotExistsAsync("goodfooddb");
        _cn = _db.GetContainer("menu");
    }

    [KernelFunction("GreetCustomer")]
    [Description("Show the current menu and Greets the customer with a welcome message.")]
    public async Task<dynamic> GreetCustomerAsync()
    {
        return "Welcome to GoodFood! How can I help you today?";
    }

    [KernelFunction("get_menu")]
    [Description("Gets the list of menu items being served at the current time. " +
        "A simple text-based format with well-aligned columns the list of menu items including columns for Menu Item ID, Name, and Price. " +
        "Keeps the columns neatly aligned for readability")]
    [return: Description("A list of menu items")]
    public async Task<FoodMnu> GetMenuItemAsync()
    {
        try
        {
            _cn = _db.GetContainer("menu");
            var currentTime = DateTime.Now.TimeOfDay;
            string menuTime = currentTime switch
            {
                _ when currentTime >= new TimeSpan(4, 0, 0) && currentTime < new TimeSpan(11, 0, 0) => "breakfast",
                _ when currentTime >= new TimeSpan(11, 0, 0) && currentTime < new TimeSpan(15, 0, 0) => "Lunch",
                _ => "Dinner"
            };

            var query = _cn.GetItemQueryIterator<FoodMnu>(
                new QueryDefinition("SELECT * FROM c WHERE c.menuid = @menuid")
                .WithParameter("@menuid", menuTime));

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                FoodMnu menu = response.Resource.FirstOrDefault();
                if (menu != null)
                {
                    return menu;
                }
            }
            return null;
        }
        catch (CosmosException ex)
        {
            Console.WriteLine($"CosmosDB Error: {ex.Message}");
            return null;
        }
    }


    [KernelFunction("PlaceOrder")]
    [Description("Places an order and calculates the total amount. " +
        "Make sure to collect the customer name. Confirm with the customer before proceeding." +
        "Only take orders for items that are on the current menu. " +
        "Complete the order by providing to total amount and directing customer to the next windows for payments." +
        "Move to the next customer")]
    public async Task<string> PlaceOrderAsync(string customerName, int[] menuItemIds, int[] quantities)
    {
        try
        {

            var menu = await GetMenuItemAsync();
            if (menu == null || menu.List == null || !menu.List.Any())
            {
                return "No menu items are currently available to order.";
            }

            _cn = _db.GetContainer("order");
            var availableMenuItems = menu.List.ToDictionary(item => item.MenuItemId, item => item.Price);
            var orderDetails = new List<OrderDetail>();
            decimal totalAmount = 0;

            for (int i = 0; i < menuItemIds.Length; i++)
            {
                if (!availableMenuItems.ContainsKey(menuItemIds[i]))
                {
                    return $"Menu item ID {menuItemIds[i]} is not available on the current menu.";
                }

                decimal price = availableMenuItems[menuItemIds[i]];
                decimal subtotal = price * quantities[i];

                orderDetails.Add(new OrderDetail
                {
                    orderdetailid = Guid.NewGuid().ToString(),
                    menuitemid = menuItemIds[i],
                    quantity = quantities[i],
                    unitprice = price,
                    subtotal = subtotal
                });

                totalAmount += subtotal;
            }

            var newOrder = new Order
            {
                orderid = Guid.NewGuid().ToString(),
                orderdate = DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss"),
                itemsnumber = orderDetails.Count,
                total = totalAmount,
                customernickname = customerName,
                orderdetails = orderDetails,
                id = Guid.NewGuid().ToString()
            };

            await _cn.CreateItemAsync(newOrder, new PartitionKey(newOrder.orderid));

            return $"Your order has been placed successfully! Your total amount is ${totalAmount}. Order ID: {newOrder.orderid}";
        }
        catch (CosmosException ex)
        {
            //Console.WriteLine($"CosmosDB Error: {ex.Message}");
            return "An error occurred while placing your order. Please try again later.";
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Error: {ex.Message}");
            return "An unexpected error occurred while processing your order.";
        }
    }

    [KernelFunction("MoveToNextCustomer")]
    [Description("Move to next customer, Show the current menu and Greets the customer with a welcome message.")]
    public async Task<dynamic> MoveTotheNextCustomer()
    {
        await Task.Delay(3000);
        Console.Clear();
        return "Welcome to GoodFood! How can I help you today?";
    }

}

public class FoodMnu
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

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

    [JsonPropertyName("id")]
    public string id { get; set; }
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



