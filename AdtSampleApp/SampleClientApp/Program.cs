using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SampleClientApp
{
    public class Program
    {
        private static DigitalTwinsClient client;

        static async Task Main()
        {
            Uri adtInstanceUrl;
            try
            {
                // Read configuration data from the 
                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();
                adtInstanceUrl = new Uri(config["instanceUrl"]);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is UriFormatException)
            {
                Log.Error($"Could not read configuration. Have you configured your ADT instance URL in appsettings.json?");
                throw;
            }

            Log.Ok("Authenticating...");
            try
            {
                var credential = new DefaultAzureCredential();
                client = new DigitalTwinsClient(adtInstanceUrl, credential);

                // Make a call to the API to force authentication to happen here
                client.Query("SELECT * FROM DIGITALTWINS");
            }
            catch (AuthenticationFailedException e)
            {
                Log.Error($"Authentication failed: {e.Message}.");
                Log.Alert($"Refer to https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.2.1/sdk/identity/Azure.Identity/README.md#authenticate-the-client on how to authenticate to Azure.");
                return;
            }

            Log.Ok($"Service client created – ready to go");

            var CommandLoopInst = new CommandLoop(client);
            await CommandLoopInst.CliCommandInterpreter();
        }
    }
}
