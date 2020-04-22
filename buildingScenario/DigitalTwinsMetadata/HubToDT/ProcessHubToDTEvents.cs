// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using ADTApi;
using ADTApi.Models;
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

namespace HubToDT
{
    /*
     * This class processes telemetry events from IoT Hub, reads temperature of a device, 
     * sets the "Temperature" property of the device with the value of the telemetry,
     * Finds the room that contains the device and sets the room Temperature property
     * to the latest telemetry value.
     */
    public class ProcessHubToDTEvents
    {
        const string AdtAppId = "0b07f429-9f4b-4714-9392-cc5e8e80c8b0";
        const string AdtInstanceUrl = "https://<your-adt-instance-hostName>";
        static AzureDigitalTwinsAPI client = null;

        [FunctionName("ProcessHubToDTEvents")]
        public async void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            // After this is deployed, you need to turn the Managed Identity Status to "On", 
            // Grab Object Id of the function and assigned "Azure Digital Twins Owner (Preview)" role to this function identity
            // in order for this function to be authorized on ADT APIs.

            //log.LogInformation(eventGridEvent.Data.ToString());
            await Authenticate(log);
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
                        await UpdateTwinProperty(deviceId, "/Temperature", temperature, log);

                        // Find parent using incoming relationships
                        // Update parent twin
                        //string parentId = await FindParent(deviceId, log);
                        string parentId = await FindParentByQuery(deviceId, log);
                        if (parentId != null)
                        {
                            await UpdateTwinProperty(parentId, "/Temperature", temperature, log);
                        }

                    }
                }
                catch (Exception e)
                {
                    log.LogError($"Error in ingest function: {e.Message}");
                }
            }
        }

        public static async Task<string> FindParent(string childId, ILogger log)
        {
            // Find parent using incoming relationships
            try
            {
                IncomingEdgeCollection relPage = await client.DigitalTwins.ListIncomingEdgesAsync(childId);
                // Just using the first page for this sample
                if (relPage != null)
                {
                    IncomingEdge ie = relPage.Value.FirstOrDefault();
                    return (ie.SourceId);
                }
            }
            catch (ErrorResponseException exc)
            {
                log.LogInformation($"*** Error in retrieving parent:{exc.Response.StatusCode}");
            }
            return null;
        }

        public static async Task<string> FindParentByQuery(string childId, ILogger log)
        {
            string query = $"SELECT Parent " +
                            $"FROM digitaltwins Parent " +
                            $"JOIN Child RELATED Parent.contains " +
                            $"WHERE Child.$dtId = '" + childId + "'";
            log.LogInformation($"Query: {query}");
            QuerySpecification queryRequest = new QuerySpecification(query);
            try
            {
                QueryResult result = await client.Query.QueryTwinsAsync(queryRequest);
                JObject parentTwin = (JObject)JsonConvert.DeserializeObject(result.Items[0].ToString());
                return (string)parentTwin["Parent"]["$dtId"];
            }
            catch (ErrorResponseException exc)
            {
                log.LogInformation($"*** Error in retrieving parent:{exc.Response.StatusCode}");
            }
            return null;
        }

        public static async Task UpdateTwinProperty(string twinId, string propertyPath, object value, ILogger log)
        {
            // If the twin does not exist, this will log an error
            try
            {
                // Update twin property
                List<Dictionary<string, object>> ops = new List<Dictionary<string, object>>();
                ops.Add(new Dictionary<string, object>()
                {
                    { "op", "replace"},
                    { "path", propertyPath},
                    { "value", value}
                });
                await client.DigitalTwins.UpdateAsync(twinId, ops);
            }
            catch (ErrorResponseException exc)
            {
                log.LogError($"Error: {exc.Response.StatusCode}");
            }
        }

        public async static Task Authenticate(ILogger log)
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            string accessToken = await azureServiceTokenProvider.GetAccessTokenAsync(AdtAppId);

            var wc = new System.Net.Http.HttpClient();
            wc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            try
            {
                TokenCredentials tk = new TokenCredentials(accessToken);
                client = new AzureDigitalTwinsAPI(tk)
                {
                    BaseUri = new Uri(AdtInstanceUrl)
                };
                log.LogInformation($"ADT service client connection created.");
            }
            catch (Exception e)
            {
                log.LogError($"ADT service client connection failed.");
            }
        }
    }
}
