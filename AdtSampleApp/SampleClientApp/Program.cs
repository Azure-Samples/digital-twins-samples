using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System;
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
            catch (Exception)
            {
                Log.Error($"Could not read configuration. Have you configured your ADT instance URL in appsettings.json?");
                return;
            }

            Log.Ok("Authenticating...");
            try
            {
                var credential = new DefaultAzureCredential();
                client = new DigitalTwinsClient(adtInstanceUrl, credential);
                // force authentication to happen here
                try
                {
                    client.GetDigitalTwin("---");
                }
                catch (RequestFailedException)
                {
                    // As we are intentionally try to retrieve a twin that is most likely not going to exist, this exception is expected
                    // We just do this to force the authentication library to authenticate ahead
                }
                catch (Exception e)
                {
                    Log.Error($"Authentication or client creation error: {e.Message}");
                    Log.Alert($"Have you checked that the configuration in serviceConfig.json is correct?");
                    Environment.Exit(0);
                }
            }
            catch (Exception e)
            {
                Log.Error($"Authentication or client creation error: {e.Message}");
                Log.Alert($"Have you checked that the configuration in serviceConfig.json is correct?");
                Environment.Exit(0);
            }

            Log.Ok($"Service client created – ready to go");

            var CommandLoopInst = new CommandLoop(client);
            await CommandLoopInst.CliCommandInterpreter();
        }
    }
}
