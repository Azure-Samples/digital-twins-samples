using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SampleClientApp
{
    public class Program
    {
        private static DigitalTwinsClient client;

        static async Task Main()
        {
            SetWindowSize();

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
                Log.Error($"Could not read configuration. Have you configured your ADT instance URL in appsettings.json?\n\nException message: {ex.Message}");
                return;
            }

            Log.Ok("Authenticating...");
            var credential = new DefaultAzureCredential();
            client = new DigitalTwinsClient(adtInstanceUrl, credential);

            Log.Ok($"Service client created – ready to go");

            var CommandLoopInst = new CommandLoop(client);
            await CommandLoopInst.CliCommandInterpreter();
        }

        static void SetWindowSize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int width = Math.Min(Console.LargestWindowWidth, 150);
                int height = Math.Min(Console.LargestWindowHeight, 40);
                Console.SetWindowSize(width, height);
            }
        }
    }
}
