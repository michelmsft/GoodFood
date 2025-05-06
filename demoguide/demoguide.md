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

**Note:** Below demo steps should be used **as a guideline** for doing your own demos. Please consider contributing to add additional demo steps.
</div>

[comment]: <> (this is the section for the Tip: item; consider adding a Tip, or remove the section between <div> and </div> if there is no tip)

***
### 1. What Resources are getting deployed
<add a one-paragraph lengthy description of what the scenario is about, and what is getting deployed>

Provide a bullet list of the Resource Group and all deployed resources with name and brief functionality within the scenario. 

* rg-%azdenvironmentname - Azure Resource Group.
* TMLABAppSvcPlan-%region% - Azure App Service Plan in each region
* TMLABWebApp-%region% - Azure App Service with static HTML webpage in each region
* TMProfile - Traffic Manager Profile with endpoints

<add a screenshot of the deployed Resource Group with resources>

<img src="https://raw.githubusercontent.com/petender/azd-tdd-starter/refs/heads/main/demoguide/TM/screenshot1.png" alt="Traffic Manager Resource Group" style="width:70%;">
<br></br>

<img src="https://raw.githubusercontent.com/petender/azd-tdd-starter/refs/heads/main/demoguide/TM/screenshot2.png" alt="Traffic Manager Profile with Endpoints" style="width:70%;">
<br></br>

<img src="https://raw.githubusercontent.com/petender/azd-tdd-starter/refs/heads/main/demoguide/TM/screenshot3.png" alt="Sample WebApp" style="width:70%;">
<br></br>

### 2. What can I demo from this scenario after deployment

Provide clear step-by-step instructions on what can be demoed after the scenario got deployed. If your demo requires additional manual steps to configure or update settings or make changes to the deployed resources, please mention it here.

Add screenshots where relevant. The can be stored in their own subfolder under the demoguide folder.



[comment]: <> (this is the closing section of the demo steps. Please do not change anything here to keep the layout consistant with the other demoguides.)
<br></br>
***
<div style="background: lightgray; 
            font-size: 14px; 
            color: black;
            padding: 5px; 
            border: 1px solid lightgray; 
            margin: 5px;">

**Note:** This is the end of the current demo guide instructions.
</div>



