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

using Azure.Iot.DigitalTwins;
using Azure.Iot.DigitalTwins.Edges;

namespace SampleFunctionsApp
{
    /*
     * This class processes telemetry events from IoT Hub, reads temperature of a device, 
     * sets the "Temperature" property of the device with the value of the telemetry,
     * Finds the room that contains the device and sets the room Temperature property
     * to the latest telemetry value.
     */
    public class ProcessHubToDTEvents
    {
        const string adtAppId = "https://digitaltwins.azure.net";
        private static string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        static DigitalTwinsClient client = null;

        [FunctionName("ProcessHubToDTEvents")]
        public async void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            // After this is deployed, you need to turn the Managed Identity Status to "On", 
            // Grab Object Id of the function and assigned "Azure Digital Twins Owner (Preview)" role to this function identity
            // in order for this function to be authorized on ADT APIs.

            //log.LogInformation(eventGridEvent.Data.ToString());
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
                        // Telemetry message format
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

                        // Reading deviceId from message headers
                        log.LogInformation(eventGridEvent.Data.ToString());
                        JObject job = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
                        string deviceId = (string)job["systemProperties"]["iothub-connection-device-id"];
                        log.LogInformation($"Found device: {deviceId}");

                        // Extracting temperature from device telemetry
                        byte[] body = System.Convert.FromBase64String(job["body"].ToString());
                        var value = System.Text.ASCIIEncoding.ASCII.GetString(body);
                        var bodyProperty = (JObject)JsonConvert.DeserializeObject(value);
                        var temperature = bodyProperty["Temperature"];
                        log.LogInformation($"Device Temperature is:{temperature}");

                        // Update device Temperature property
                        await AdtUtilities.UpdateTwinProperty(client, deviceId, "/Temperature", temperature, log);

                        // Find parent using incoming relationships
                        // Update parent twin
                        //string parentId = await FindParent(deviceId, log);
                        string parentId = await AdtUtilities.FindParentByQuery(client, deviceId, log);
                        if (parentId != null)
                        {
                            await AdtUtilities.UpdateTwinProperty(client, parentId, "/Temperature", temperature, log);
                        }

                    }
                }
                catch (Exception e)
                {
                    log.LogError($"Error in ingest function: {e.Message}");
                }
            }
        }

    }
}
