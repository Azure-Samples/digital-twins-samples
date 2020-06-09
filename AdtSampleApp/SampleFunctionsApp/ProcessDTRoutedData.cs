// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.DigitalTwins.Core;
using System.Net.Http;
using Azure.Core.Pipeline;

namespace SampleFunctionsApp
{
    /*
    * This class processes property change notification from ADT, reads twin room id that's associated to the event, 
    * finds the parent floor twin that contains this twin and sets the parent Temperature property
    * to the value from the notification.
    */
    public static class ProcessDTRoutedData
    {
        const string adtAppId = "https://digitaltwins.azure.net";
        private static string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("ProcessDTRoutedData")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            // After this is deployed, you need to turn the Identity Status "On", 
            // Grab Object Id of the function and assigned "Azure Digital Twins Owner (Preview)" role to this function identity
            // in order for this function to be authorize on ADT APIs.

            DigitalTwinsClient client = null;
            log.LogInformation("start execution");
            // Authenticate on ADT APIs
            try
            {
                ManagedIdentityCredential cred = new ManagedIdentityCredential(adtAppId);
                client = new DigitalTwinsClient(new Uri(adtInstanceUrl), cred, new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
                log.LogInformation($"ADT service client connection created.");
            }
            catch (Exception e)
            {
                log.LogError($"ADT service client connection failed. " + e.ToString());
                return;
            }

            if (client != null)
            {
                try
                {
                    if (eventGridEvent != null && eventGridEvent.Data != null)
                    {
                        string twinId = eventGridEvent.Subject.ToString();
                        JObject message = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());

                        log.LogInformation("Reading event from " + twinId + ": " + eventGridEvent.EventType.ToString()+ ": "+ message["data"]);
                        
                        //Find and update parent Twin
                        string parentId = await AdtUtilities.FindParent(client, twinId, "contains", log);
                        if (parentId != null)
                        {
                            // Read properties which values have been changed in each operation
                            foreach (var operation in message["data"]["patch"])
                            {
                                string opValue = (string)operation["op"];
                                if (opValue.Equals("replace"))
                                {
                                    string propertyPath = ((string)operation["path"]);
                                    string propertyValue = (string)operation["value"];

                                    if (propertyPath.Equals("/Temperature"))
                                    {
                                        await AdtUtilities.UpdateTwinProperty(client, parentId, "replace", propertyPath, "double", propertyValue, log);
                                    }
                                }
                            }
                        }

                    }
                }
                catch (Exception e)
                {
                    log.LogError($"{e}");
                }
            }
        }
        
    }
}
