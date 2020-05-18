// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Linq;
using Azure;
using Azure.Identity;

using Azure.DigitalTwins.Core;
using Azure.DigitalTwins.Core.Models;


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
        static DigitalTwinsClient client = null;

        [FunctionName("ProcessDTRoutedData")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation("start execution");
            // After this is deployed, you need to turn the Identity Status "On", 
            // Grab Object Id of the function and assigned "Azure Digital Twins Owner (Preview)" role to this function identity
            // in order for this function to be authorize on ADT APIs.

            // Authenticate on ADT APIs
            try
            {
                ManagedIdentityCredential cred = new ManagedIdentityCredential(adtAppId);
                client = new DigitalTwinsClient(new Uri(adtInstanceUrl), cred);
                log.LogInformation($"ADT service client connection created.");
            }
            catch (Exception e)
            {
                log.LogError($"ADT service client connection failed.");
                return;
            }

            if (client != null)
            {
                try
                {
                    if (eventGridEvent != null && eventGridEvent.Data != null)
                    {
                        #region Open this region for message format information
                        // Known Issue: you cannot read the header event type in telemetry or notifications, 
                        // you cannot read notification properties for now until CE format it's implemented
                        // Therefore you have to parse the notification for "Operations" (for now)
                        // Read property change events, format looks like this
                        //{
                        //  "data":
                        //  {
                        //    "TwinId": "room1",
                        //    "Operations": 
                        //    [
                        //        {"op": "replace", "path": "/Temperature", "value": 70},
                        //        {"op": "replace",  "path": "/Humidity","value": 49 }
                        //    ]
                        //  }
                        // }
                        // Device Telemetry message
                        //{
                        //  "properties": { },
                        //  "systemProperties": 
                        // {
                        //    "iothub-connection-device-id": "thermostat1",
                        //    "iothub-connection-auth-method": "{\"scope\":\"device\",\"type\":\"sas\",\"issuer\":\"iothub\",\"acceptingIpFilterRule\":null}",
                        //    "iothub-connection-auth-generation-id": "637199981642612179",
                        //    "iothub-enqueuedtime": "2020-03-18T18:35:08.269Z",
                        //    "iothub-message-source": "Telemetry"
                        //  },
                        //  "body": "eyJUZW1wZXJhdHVyZSI6NzAuOTI3MjM0MDg3MTA1NDg5fQ=="
                        //}
                        #endregion

                        string evt = eventGridEvent.Data.ToString();
                        log.LogInformation(evt);
                        JObject message = (JObject)JsonConvert.DeserializeObject(evt);

                        // Read twin id
                        string twinId = (string) message["TwinId"];
                        log.LogInformation($"Found updates on twin: {twinId}");

                        // Validate "Operations" and TwinId
                        if ((message["Operations"] == null || message["Operations"].Count() == 0) && String.IsNullOrEmpty(twinId))
                        {
                            log.LogInformation($"No twin property change events");
                        }
                        
                        //string parent = await FindParent(twinId, "contains", log);
                        string parentId = await AdtUtilities.FindParentByQuery(client, twinId, log);
                        if (parentId != null)
                        {
                            // Read properties which values have been changed in each operation
                            foreach (var operation in message["Operations"])
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
                    log.LogError($"*** Unable to create client connection {e}");
                }
            }
        }
        
    }
}
