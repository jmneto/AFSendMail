# Sending emails from Azure Database for PostgreSQL and Azure SQL Database with Azure Functions and SendGrid on Azure

## Introduction
Applications may need to send emails directly from the database server engine in response to completing a stored procedure execution, a maintenance task, send query results, or any other database engine operation.  

Migrated databases from Oracle or other database services to Azure Database for PostgreSQL or Azure SQL Database may need to keep the capacity to send emails from the database engine to maintain application feature parity.  

This project describes how to use Azure Functions and the SendGrid Service on Azure to create a solution for sending emails from Azure Database for PostgreSQL or Azure SQL Database.   

The accompanying .Net C# solution leverages Azure Functions to create a reliable, scalable, asynchronous, and lightweight email solution.  


## Solution Architecture
The solution uses SendGrid on Microsoft Azure to provide reliable email services, and Azure Functions provide the necessary code to invoke the SendGrid API.  

The cloud infrastructure provides up-to-date servers to your application without the need to provision Virtual Machines or any other solution and with reduced costs. 

The solution can scale vertically or horizontally by adding more resources or executing multiple Azure Functions in parallel. The frequency for pooling the database for new messages also can be adjusted. Nevertheless, all the source code is provided for custom expansion.

The integration with the databases is based on a table queue where we insert one row for each message we want to send. You can have as many database threads writing to the queue tables as well as multiple Azure Functions sending emails concurrently from the same queues.

The component that delivers email runs outside of the database engine, in a separate process. The database will continue to queue e-mail messages even if the external process stops or fails.

## Setup 
1. Create a SendGrid Account
    * Sign in to the Azure portal
    * Create a SendGrid resource
    * Create a SendGrid account and obtain an API KEY
  
      After you follow the online instructions to create your SendGrid resource click “Manage” to manage send grid and continue the process to obtain an API KEY, including the Sender Identity confirmation that is now required.

2.	Create a queue table 
    * You need to create one queue table on each database you want to send messages or create a table on a central database and have other databases write to this table.  
    * Processed rows are deleted from the queue table after the email is sent. You can create triggers on the queue table if you want to save history.
    * The message is deleted from the queue table only after its guaranteed delivery to the SendGrid API. Hence in case of API failure no messages will be lost.
    * You can run as many Azure Functions reading from the same queue tables. There is no conflict, and no emails will not be lost or sent more than once.

        ```    
        Create Queue Table Script for Azure Database for PostgreSQL
        CREATE TABLE SendGridQueue (
            Sender                     varchar(200),
            Tos_comma_separated        varchar(2000),
            Subject                    varchar(200),
            PlainTextContent           varchar(4000),
            HtmlContent                varchar(4000),
            id                         int GENERATED ALWAYS AS IDENTITY,
            PRIMARY KEY (id)
        );
        
        Create Queue Table Script for Azure SQL Database
        CREATE TABLE SendGridQueue (
            Sender                     varchar(200),
            Tos_comma_separated        varchar(2000),
            Subject                    varchar(200),
            PlainTextContent           varchar(4000),
            HtmlContent                varchar(4000),
            id                         int PRIMARY KEY IDENTITY
        );
        ```
    * Here is how to insert an email message into the Queue Table for sending:

        ```
        INSERT INTO sendgridqueue (sender, tos_comma_separated, subject, plaintextcontent, htmlcontent)
        VALUES ('test@example.com', 'test@example.com, test@live.com', 'Test Email Subject', 'Test Email PlainText Body', '<strong>Test Email HTML Body</strong>');
        ```

        Fields:

        Sender	| The “from:” email we are sending
        -| -
        tos_comma_separated	| List of “to:” emails we are sending separate by commas
        Subject	| The email subject
        Plaintextcontent	| Plain text email body
        Htmlcontent	| Html email body


3.	Create and Publish your Azure Function with Visual Studio
    * Solution is a C# project of project type Azure Functions and the Target Framework is .NET CORE 3.1
    * Configure for API Key and Connection String in the solution project file local.settings.json

    * Your connection string will determine if we are using ADO.NET for Azure SQL Database or Azure Database for PostgreSQL.

    Sample connections strings below:

    ```
    Server={your_servername}.postgres.database.azure.com;Database={your_database};Port=5432;User Id={your_username}@{your_servername};Password={your_password};Ssl Mode=Require;
    ```

    Or 

    ```
    Server=tcp:{your_servername}.database.windows.net,1433;Initial Catalog={your_database};Persist Security Info=False;User ID={your_username};Password={your_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
    ```

    Note:
    Only one type of database connection is allowed per Azure Function. If you have multiple databases servers, you must use one Azure Function for each.

    This is defined in this fragment of the code:

    ```
    // Gather the connection string
    string connString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
    
    if (connString == null)
        throw new Exception("CONNECTION_STRING is undefined ");

    // Based of connection string hint determine if we are connecting to Azure SQL DB or Azure DB for PostgreSQL
    if (connString.ToLower().Contains("postgres.database.azure.com"))
    {
        PostgreSQLDBHelper.RetrieveSendMessages(connString, log);
    }
    else if (connString.ToLower().Contains("database.windows.net"))
    {
        SQLDBHelper.RetrieveSendMessages(connString, log);
    }
    ```

    * In Visual Studio 2019 Select the Option to Publish to Azure. Before Publishing for the First Time, you will have to configure your Azure Environment to run Azure Functions, including selecting a Resource Group, Azure Function Plan and Storage Account.  

    
      More details:

        [Quickstart: Create your first function in Azure using Visual Studio](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-your-first-function-visual-studio)  
        [Develop Azure Functions using Visual Studio](https://docs.microsoft.com/en-us/azure/azure-functions/functions-develop-vs?tabs=in-process)  
        [Deployment technologies in Azure Functions](https://docs.microsoft.com/en-us/azure/azure-functions/functions-deployment-technologies)

        Note:

        “Always On “option is required. To make sure your Azure Function continues running constantly, you must select App Service Plan when creating the host for the Azure Function. On an App Service plan, the functions runtime goes idle after a few minutes of inactivity, so only HTTP triggers will "wake up" your functions. Always on is available only on an App Service plan. Make sure you select App Service Plan to make sure your Function stays on and running.

        [Enable Always On when running on dedicated App Service Plan](https://github.com/Azure/Azure-Functions/wiki/Enable-Always-On-when-running-on-dedicated-App-Service-Plan)


4.	Configure SENDGRID_API_KEY and CONNECTION_STRING in Azure Portal

    * The solution requires two Application Settings to be configured:

        CONNECTION_STRING  
        SENDGRID_API_KEY

    * Open your function configuration screen and add Application Settings SENDGRID_API_KEY and CONNECTION_STRING


    * Configure the interval for SendGrid Azure Function to pool the Queue table and send emails.

        The timer trigger is defined in this portion of the code and is defined as a [NCRONTAB](https://github.com/atifaziz/NCrontab) expression. 

        ```
         public static void Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer, ILogger log)
        ```
    
5. Monitoring

    * You can use the Azure Function console function to follow operation. (Application insights must be activated)

    * Azure Function log can also be seen in Visual Studio Cloud Explorer






