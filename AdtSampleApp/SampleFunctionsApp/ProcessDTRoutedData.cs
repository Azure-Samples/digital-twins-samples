// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace SampleFunctionsApp
{
    // This class processes property change notification from ADT, reads twin room id that's associated to the event,
    // finds the parent floor twin that contains this twin and sets the parent Temperature property
    // to the value from the notification.
    public static class ProcessDTRoutedData
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static string adtServiceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        [FunctionName("ProcessDTRoutedData")]
        public static async Task Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation("Start execution");
            // After this is deployed, you'll need to turn the Azure Function Identity Status "On", 
            // grab Object ID of the function, and assign "Azure Digital Twins Owner (Preview)" role to this function identity
            // in order for this function to be authorized on ADT APIs.
            //
            // If you are following "Tutorial: Connect an end-to-end solution" in the Azure Digital Twins documentation, 
            // you have done this already with an equivalent CLI step in the "Assign permissions to the function app" section.

            DigitalTwinsClient client;
            // Authenticate on ADT APIs
            try
            {
                var credentials = new DefaultAzureCredential();
                client = new DigitalTwinsClient(new Uri(adtServiceUrl), credentials, new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
                log.LogInformation("ADT service client connection created.");
            }
            catch (Exception e)
            {
                log.LogError($"ADT service client connection failed. {e}");
                return;
            }

            if (client != null)
            {
                if (eventGridEvent != null && eventGridEvent.Data != null)
                {
                    string twinId = eventGridEvent.Subject.ToString();
                    JObject message = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());

                    log.LogInformation($"Reading event from {twinId}: {eventGridEvent.EventType}: {message["data"]}");

                    //Find and update parent Twin
                    string parentId = await AdtUtilities.FindParentAsync(client, twinId, "contains", log);
                    if (parentId != null)
                    {
                        // Read properties which values have been changed in each operation
                        foreach (var operation in message["data"]["patch"])
                        {
                            string opValue = (string)operation["op"];
                            if (opValue.Equals("replace"))
                            {
                                string propertyPath = ((string)operation["path"]);

                                if (propertyPath.Equals("/Temperature"))
                                {
                                    await AdtUtilities.UpdateTwinPropertyAsync(client, parentId, propertyPath, operation["value"].Value<float>(), log);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
