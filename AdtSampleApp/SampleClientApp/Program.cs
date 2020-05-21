using Microsoft.Identity.Client;
using Azure.Identity;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

using Azure.DigitalTwins.Core;
using Azure.DigitalTwins.Core.Serialization;
using Azure.DigitalTwins.Core.Models;
using System.Runtime.InteropServices;
using Azure;


//
// Todo: Replace use of newtonsoft to Json.Text.Json
// Todo: Remove the hack to force authentication right at the beginning...
// Todo: Have the build move the dtdl files to the exe directory?
// Todo: Need to decide between config and env variables
//

namespace SampleClientApp
{
    public class Program
    {
        // Properties to establish connection
        // Please copy the file serviceConfig.json.TEMPLATE to serviceConfig.json 
        // and set up these values in the config file
        private static string clientId;
        private static string tenantId;
        private static string adtInstanceUrl;

        const string adtAppId = "https://digitaltwins.azure.net";
        
        private static DigitalTwinsClient client;

        static string[] scopes = new[] { adtAppId + "/.default" };

        static async Task Main()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int width = Math.Min(Console.LargestWindowWidth, 150);
                int height = Math.Min(Console.LargestWindowHeight, 40);
                Console.SetWindowSize(width, height);
            }
            try
            {
                // Read configuration data from the 
                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("serviceConfig.json", true, true)
                    .Build();
                clientId = config["clientId"];
                tenantId = config["tenantId"];
                adtInstanceUrl = config["instanceUrl"];
            } catch (Exception e)
            {
                Log.Error($"Could not read service configuration file serviceConfig.json");
                Log.Alert($"Please copy serviceConfig.json.TEMPLATE to serviceConfig.json");
                Log.Alert($"and edit to reflect your service connection settings");
                Environment.Exit(0);
            }
            
            Log.Ok("Authenticating...");
            try
            {
                var credential = new InteractiveBrowserCredential(tenantId, clientId);
                client = new DigitalTwinsClient(new Uri(adtInstanceUrl), credential);
                // force authentication to happen here
                // (for some reason this only happens on first call)
                try
                {
                    client.GetDigitalTwin("---");
                }
                catch (RequestFailedException rex)
                {

                }
                catch (Exception e)
                {
                    Log.Error($"Authentication or client creation error: {e.Message}");
                    Environment.Exit(0);
                }
            } catch(Exception e)
            {
                Log.Error($"Authentication or client creation error: {e.Message}");
                Environment.Exit(0);
            }
            
            Log.Ok($"Service client created – ready to go");

            CommandLoop CommandLoopInst = new CommandLoop(client);
            await CommandLoopInst.CliCommandInterpreter();
        }

    }
}
