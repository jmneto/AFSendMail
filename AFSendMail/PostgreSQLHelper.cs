using System;
using System.Data;
using Npgsql;
using Microsoft.Extensions.Logging;

namespace AFSendMail
{
    static internal class PostgreSQLDBHelper
    {
        static public void RetrieveSendMessages(string connString, ILogger log)
        {
            // Connection String
            if (connString == null)
                throw new Exception("CONNECTION_STRING is undefined");

            // Data Set to return the messages for procesing
            var ds = new DataSet();

            using (var conn = new NpgsqlConnection(connString))
            {
                // Opening connection
                conn.Open();

                ds.DataSetName = String.Format("{0}/{1}", conn.DataSource, conn.Database);

                // Beginnig Transaction
                using (var tran = conn.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    string transavepoint = GetRandomString();
                    try
                    {
                        //  Select to retrieve the rows (skip rows locked by other instances or the application addding emails) batchsize if 1000
                        using (var command = new NpgsqlCommand("SELECT sender, tos_comma_separated, subject, plaintextcontent, htmlcontent, id FROM sendgridqueue FOR UPDATE SKIP LOCKED LIMIT 1000", conn))
                        {
                            // Setup transaction
                            command.Transaction = tran;

                            // Execute command to a dataset
                            var da = new NpgsqlDataAdapter(command);
                            da.Fill(ds);

                            // read from the dataset / Send the email within the transaction
                            foreach (DataRow r in ds.Tables[0].Rows)
                            {
                                // Save Transaction point
                                tran.Save(transavepoint = GetRandomString());

                                // Create new Email Message
                                string from = (string)r["sender"];
                                string to_list = (string)r["tos_comma_separated"];
                                string subject = (string)r["subject"];
                                string plainTextContent = (string)r["plaintextcontent"];
                                string htmlContent = (string)r["htmlcontent"];

                                // Send it
                                SendMailHelper.SendMailAsync(from, to_list, subject, plainTextContent, htmlContent, log).Wait();

                                // Delete processed rows
                                command.CommandText = String.Format("DELETE FROM sendgridqueue WHERE id = {0}", (int)r["id"]);
                                command.ExecuteNonQuery();
                            }
                            tran.Commit();
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            tran.Rollback(transavepoint);
                            tran.Commit();
                        }
                        finally
                        {
                            throw new Exception("PostgreSQLDBHelper exception", ex);
                        }
                    }
                }
            }
            
            log.LogInformation("Database {0} emails retrieved and sent: {1}", ds.DataSetName, ds.Tables[0].Rows.Count);

            return;
        }

        static string GetRandomString()
        {
            string str = String.Empty;
            for (int i = 0; i < 10; i++)
                str += (char)('a' + new Random().Next(0, 26));

            return str;
        }

    }
}
