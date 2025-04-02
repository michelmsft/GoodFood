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

You can use Azure CLI command sequence to create an Azure Cosmos DB account, a database named `goodfooddb`, and two containers (`menu` and `order`) with their respective partition keys.

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

## Phase 2: FrontEnd Fast Food ops using a .Net Console app 

The current code is using **C#**, **Azure OpenAI**, **Semantic Kernel**, and **CosmosDB** to allow customers to view the menu, place orders, and interact with the assistant via chat or speech recognition.

### **Key Features and Functionality**

#### **1. Loading Configuration Settings**
- Reads API keys, endpoints, and CosmosDB credentials from `appsettings.json`.
- If any value is missing, the program exits.

#### **2. Building the AI Kernel**
- Uses `AzureOpenAIChatCompletion` to create an AI-powered chat assistant.
- The assistant is built using **Microsoft Semantic Kernel**.
- Adds a plugin (`GoodFoodPlugin`) to handle order-related functions.

#### **3. Setting Up the Chat Assistant**
- Initializes chat history and adds a system message defining the AI's role:
  > "You are a virtual drive-thru assistant at GoodFood..."
- Starts an interactive chat session where:
  - The user enters a message.
  - The AI responds and updates chat history.
  - The response is **spoken aloud** using `SpeechSynthesizer`.
  - The AI continues the conversation until the user exits.

#### **4. The `GoodFoodPlugin` Class (Manages Orders & Menu)**
This plugin interacts with **Azure CosmosDB** to:
- Fetch the **current menu** (breakfast, lunch, or dinner).
- Place orders and calculate total cost.
- Move to the next customer.

##### **(a) `GreetCustomerAsync()`**
- Returns a simple welcome message.

##### **(b) `GetMenuItemAsync()`**
- Fetches menu items based on the time of day.
- Uses CosmosDB to retrieve menu items.

##### **(c) `PlaceOrderAsync()`**
- Receives customer name, menu item IDs, and quantities.
- Checks if items are available and calculates the total price.
- Stores the order in CosmosDB.

##### **(d) `MoveTotheNextCustomer()`**
- Clears the console and welcomes the next customer.

---

### **Key Technologies Used**
1. **Microsoft Semantic Kernel** - AI-powered conversational interface.
2. **Azure OpenAI** - Processes customer interactions.
3. **CosmosDB** - Stores menu data and customer orders.
4. **Regex & SpeechSynthesizer** - Reads and speaks responses.
5. **Dependency Injection (`IKernel`)** - Manages AI plugins.

---

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



