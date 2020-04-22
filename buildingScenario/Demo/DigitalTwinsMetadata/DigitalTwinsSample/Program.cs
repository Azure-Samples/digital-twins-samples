using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace DigitalTwinsSample
{
    public class Program
    {
        //Set the scope for API call to user.read
        private const string ClientId = "<your-ClientId>";
        private const string TenantId = "<your-TenantId>";

        const string AdtAppId = "0b07f429-9f4b-4714-9392-cc5e8e80c8b0";
        const string AdtInstanceUrl = "https://<your-dt-instance-hostname>";
        
        static string[] scopes = new[] { AdtAppId + "/.default" };

        static async Task Main()
        {
            if (AdtInstanceUrl == "https://<your-dt-instance-hostname>")
            {
                Console.WriteLine("******Please change the 'AdtInstanceUrl' string in Program.cs to reflect your Digital Twins Instance HostName******");
            }
            else
            {
                Console.WriteLine("Performing Interactive Authorization...");
                AuthHelper AuthHelperInst = new AuthHelper(scopes, AdtInstanceUrl, ClientId, TenantId);
                AuthHelperInst.Authorize().Wait();

                CommandLoop CommandLoopInst = new CommandLoop(AuthHelperInst.getClient());
                CommandLoopInst.run();
            }
        }

        
    }
}