using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;

namespace AFSendMail
{
    public static class SendMail
    {
        [FunctionName("SendMail")]
        public static void Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer, ILogger log)
        {
            try
            {
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
                else
                    throw new Exception("CONNECTION_STRING is invalid, must connect to AzureSQL Database for PostgreSQL or Azure SQL Database");
            }
            catch (Exception ex)
            {
                log.LogError(ex.ToString());
            }
        }
    }

    static class SendMailHelper
    {
        static string apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");

        // Send Mail using the SengGrid API
        static async public Task SendMailAsync(string from, string to_list, string subject, string plainTextContent, string htmlContent, ILogger log)
        {
            if (apiKey == null)
                throw new Exception("SENDGRID_API_KEY is undefined");

            // Create the Message Object
            var msg = new SendGridMessage();
            msg.From = new EmailAddress(from.Trim());
            foreach (string to in to_list.Split(","))
                msg.AddTo(new EmailAddress(to.Trim()));
            msg.Subject = subject;
            msg.PlainTextContent = plainTextContent;
            msg.HtmlContent = htmlContent;

            // Create the SendGrid Client and send the email
            var client = new SendGridClient(apiKey);

            log.LogInformation($"Sending email: {msg.Serialize().ToString()}");

            var response = await client.SendEmailAsync(msg).ConfigureAwait(false);

            log.LogInformation($"SendGrid Response: {response.StatusCode}");
            //log.LogInformation($"Headers: {response.Headers}");

            // If wanted we can throw an error if the response from SendGrid is not 200:OK
            // This will cause the failed message to be stuck in the Queue Table
            if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                throw new Exception($"SendEmailAsync response exception {response.StatusCode}");
            }
        }
    }
}
