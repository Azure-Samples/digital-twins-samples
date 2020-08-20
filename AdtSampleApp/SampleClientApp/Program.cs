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
            var credential = new DefaultAzureCredential();
            client = new DigitalTwinsClient(adtInstanceUrl, credential);

            Log.Ok($"Service client created – ready to go");

            var CommandLoopInst = new CommandLoop(client);
            await CommandLoopInst.CliCommandInterpreter();
        }
    }
}
