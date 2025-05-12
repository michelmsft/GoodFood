[comment]: <> (please keep all comment items at the top of the markdown file)
[comment]: <> (please do not change the ***, as well as <div> placeholders for Note and Tip layout)
[comment]: <> (please keep the ### 1. and 2. titles as is for consistency across all demoguides)
[comment]: <> (section 1 provides a bullet list of resources + clarifying screenshots of the key resources details)
[comment]: <> (section 2 provides summarized step-by-step instructions on what to demo)


[comment]: <> (this is the section for the Note: item; please do not make any changes here)
***
### <your scenario title here>

<div style="background: lightgreen; 
            font-size: 14px; 
            color: black;
            padding: 5px; 
            border: 1px solid lightgray; 
            margin: 5px;">

# Fast Food Drive thru Operator Ai Agent with Semantic Kernel and GPT 3.5 Turbo
### **Fast Food Drive-Thru Operator AI Agent**  

This AI-powered **Fast Food Drive-Thru Operator** leverages **Semantic Kernel** and **Azure OpenAI GPT-3.5 turbo** to streamline customer interactions, improve efficiency, and automate order processing.  
![image](https://github.com/user-attachments/assets/e61f1162-93d1-4847-a57a-bbb6de614d34)

This AI agent enhances the **drive-thru experience** with fast, intelligent, and voice-driven automation! 
</div>

[comment]: <> (this is the section for the Tip: item; consider adding a Tip, or remove the section between <div> and </div> if there is no tip)

***
### 1. Resources Being Deployed

This solution deploys a complete set of Azure services to support an intelligent, voice-enabled food ordering experience, securely hosted within a defined network boundary.

- **Conversational AI & Order Management**  
  Utilizes Azure OpenAI GPT-3.5 Turbo, integrated with the Semantic Kernel Planner, to deliver intelligent, natural conversations and automate order processing workflows.

- **NoSQL Data Storage**  
  Azure Cosmos DB provides a highly scalable, low-latency database for storing and retrieving order data and customer interactions.

- **Real-Time Voice Interaction**  
  Azure AI Speech Service (Speech Synthesis) enables the app to communicate with users via natural-sounding voice responses during the ordering process.

- **Network Security Perimeter (NSP)**  
  A logical boundary is established to isolate and secure all deployed services from unauthorized access, following best practices for perimeter-based security.

---

#### 📦 Deployed Azure Resources

| Resource Name Pattern | Description |
|-----------------------|-------------|
| `rg-%azdenvironmentname%` | Azure Resource Group to contain all resources |
| `cosmos-%suffix%`          | Azure Cosmos DB account |
| `openai-%suffix%`          | Azure OpenAI service |
| `speech-%suffix%`          | Azure AI Speech Service |
| `sp-%suffix%`              | Network Security Perimeter definition |



![image](https://github.com/user-attachments/assets/953b123c-0f35-43a3-9db2-84b98518a6ee)



### 2. What can I demo from this scenario after deployment

After deploying the solution, you can demonstrate the following interactive features:

- Start the application by running `dotnet run`, and initiate the conversation with a greeting, such as:  
  `"Hello!"`

- Ask about the available food options, for example:  
  `"What's on the menu right now?"`

- Continue the dialogue naturally, placing an order through the conversational interface.

- For full visibility during the demo, be sure to show the **Cosmos DB `views` container** in a split-screen view, so observers can see real-time data updates as the interaction progresses.

![image](https://github.com/user-attachments/assets/d2a25800-3d03-411d-b3b9-7183d05ca7c5)


