# Fast Food Drive thru Operator Ai Agent with Semantic Kernel and GPT 4o mini

### **Fast Food Drive-Thru Operator AI Agent**  

This AI-powered **Fast Food Drive-Thru Operator** leverages **Semantic Kernel** and **Azure OpenAI GPT-4o mini** to streamline customer interactions, improve efficiency, and automate order processing.  

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

### Step 4: Create the `menu` Container
```sh
az cosmosdb sql container create \
    --account-name $COSMOS_DB_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --database-name $DB_NAME \
    --name "menu" \
    --partition-key-path "/menuid" \
    --throughput 400
```

### Step 5: Create the `order` Container
```sh
az cosmosdb sql container create \
    --account-name $COSMOS_DB_ACCOUNT \
    --resource-group $RESOURCE_GROUP \
    --database-name $DB_NAME \
    --name "order" \
    --partition-key-path "/orderid" \
    --throughput 400
```


### **Steps 6: Add the Menus Manually**
1. **Create a new document** for each menu (`breakfast`, `lunch`, and `dinner`).  
2. **Copy and paste** the respective JSON data into each document.  
3. **Ensure proper formatting** by checking that the JSON structure remains intact.  


#### Step 6 - 1: Menu breakfast
```json

{
        "id": "1",
        "menuid": "breakfast",
        "startingtime": "04:00:00am",
        "endtime": "10:59:59am",
        "list": [
            {"MenuItemId": 1, "Name": "Pancakes with Syrup", "Description": "Fluffy pancakes with syrup", "Price": 5.99},
            {"MenuItemId": 2, "Name": "Scrambled Eggs with Toast", "Description": "Scrambled eggs with toast", "Price": 4.99},
            {"MenuItemId": 3, "Name": "Bacon and Egg Sandwich", "Description": "Bacon and egg sandwich", "Price": 6.99},
            {"MenuItemId": 4, "Name": "French Toast", "Description": "French toast with syrup", "Price": 5.99},
            {"MenuItemId": 5, "Name": "Breakfast Burrito", "Description": "Burrito with eggs, bacon, and cheese", "Price": 7.99},
            {"MenuItemId": 6, "Name": "Oatmeal with Fruit", "Description": "Oatmeal topped with fresh fruit", "Price": 4.99},
            {"MenuItemId": 7, "Name": "Sausage and Egg Muffin", "Description": "Muffin with sausage and egg", "Price": 5.99},
            {"MenuItemId": 8, "Name": "Yogurt Parfait", "Description": "Yogurt with granola and fruit", "Price": 3.99},
            {"MenuItemId": 9, "Name": "Bagel with Cream Cheese", "Description": "Bagel with cream cheese", "Price": 3.99},
            {"MenuItemId": 10, "Name": "Waffles with Berries", "Description": "Waffles topped with berries", "Price": 6.99}
        ]
    }
```
#### Step 6 - 1: Menu Lunch
```json

{
        "id": "2",
        "menuid": "lunch",
        "startingtime": "11:00:00am",
        "endtime": "03:59:59pm",
        "list": [
            {"MenuItemId": 11, "Name": "Cheeseburger", "Description": "Juicy beef burger with cheese", "Price": 8.99},
            {"MenuItemId": 12, "Name": "Grilled Chicken Sandwich", "Description": "Grilled chicken sandwich with lettuce and tomato", "Price": 7.99},
            {"MenuItemId": 13, "Name": "Caesar Salad", "Description": "Caesar salad with croutons and parmesan", "Price": 6.99},
            {"MenuItemId": 14, "Name": "Turkey Club Sandwich", "Description": "Turkey club sandwich with bacon and avocado", "Price": 9.99},
            {"MenuItemId": 15, "Name": "Veggie Wrap", "Description": "Wrap with assorted vegetables and hummus", "Price": 7.99},
            {"MenuItemId": 16, "Name": "Chicken Caesar Wrap", "Description": "Wrap with chicken, lettuce, and Caesar dressing", "Price": 8.99},
            {"MenuItemId": 17, "Name": "BLT Sandwich", "Description": "Bacon, lettuce, and tomato sandwich", "Price": 6.99},
            {"MenuItemId": 18, "Name": "Tuna Salad Sandwich", "Description": "Tuna salad sandwich with lettuce", "Price": 7.99},
            {"MenuItemId": 19, "Name": "BBQ Pulled Pork Sandwich", "Description": "Pulled pork sandwich with BBQ sauce", "Price": 9.99},
            {"MenuItemId": 20, "Name": "Chicken Quesadilla", "Description": "Quesadilla with chicken and cheese", "Price": 8.99}
        ]
    }
```

#### Step 6 - 3: Menu Dinner
```json

{
        "id": "3",
        "menuid": "dinner",
        "startingtime": "04:00:00pm",
        "endtime": "01:59:59am",
        "list": [
            {"MenuItemId": 21, "Name": "Grilled Steak with Vegetables", "Description": "Grilled steak with a side of vegetables", "Price": 15.99},
            {"MenuItemId": 22, "Name": "Spaghetti Bolognese", "Description": "Spaghetti with Bolognese sauce", "Price": 12.99},
            {"MenuItemId": 23, "Name": "Grilled Salmon with Rice", "Description": "Grilled salmon with a side of rice", "Price": 14.99},
            {"MenuItemId": 24, "Name": "Chicken Alfredo Pasta", "Description": "Pasta with Alfredo sauce and chicken", "Price": 13.99},
            {"MenuItemId": 25, "Name": "Beef Tacos", "Description": "Tacos with seasoned beef and toppings", "Price": 11.99},
            {"MenuItemId": 26, "Name": "Shrimp Scampi", "Description": "Shrimp scampi with garlic butter sauce", "Price": 16.99},
            {"MenuItemId": 27, "Name": "BBQ Ribs", "Description": "BBQ ribs with a side of coleslaw", "Price": 17.99},
            {"MenuItemId": 28, "Name": "Chicken Parmesan", "Description": "Chicken Parmesan with marinara sauce", "Price": 14.99},
            {"MenuItemId": 29, "Name": "Beef Stir Fry", "Description": "Beef stir fry with vegetables", "Price": 13.99},
            {"MenuItemId": 30, "Name": "Vegetable Lasagna", "Description": "Lasagna with assorted vegetables", "Price": 12.99}
        ]
    }
```

This will insert each menu item into the `menu` container in Cosmos DB.

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



