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

namespace DTRoutedData
{
    /*
    * This class processes property change notification from ADT, reads twin room id that's associated to the event, 
    * finds the parent floor twin that contains this twin and sets the parent Temperature property
    * to the value from the notification.
    */
    public static class ProcessDTRoutedData
    {
        const string AdtAppId = "0b07f429-9f4b-4714-9392-cc5e8e80c8b0";
        const string AdtInstanceUrl = "https://<your-adt-instance-hostName>";
        static AzureDigitalTwinsAPI client = null;

        [FunctionName("ProcessDTRoutedData")]
        public static async Task Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation("start execution");
            // After this is deployed, you need to turn the Identity Status "On", 
            // Grab Object Id of the function and assigned "Azure Digital Twins Owner (Preview)" role to this function identity
            // in order for this function to be authorize on ADT APIs.

            // Authenticate on ADT APIs
            await Authenticate(log);

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
                        string parentId = await FindParentByQuery(twinId, log);
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
                                        await UpdateTwinProperty(parentId, "replace", propertyPath, "double", propertyValue, log);
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

        public static async Task<string> FindParent(string child, string relname, ILogger log)
        {
            // Find parent using incoming relationships
            try
            {
                IncomingEdgeCollection relPage = await client.DigitalTwins.ListIncomingEdgesAsync(child);
                // Just using the first page for this sample
                // Find the first incoming edge of type relname
                if (relPage != null)
                {
                    foreach (IncomingEdge ie in relPage.Value)
                    {
                        if (ie.Relationship == relname)
                            return (ie.SourceId);
                    }
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
                if (result.Items.Count() == 1)
                {
                    JObject parentTwin = (JObject)JsonConvert.DeserializeObject(result.Items[0].ToString());
                    return (string)parentTwin["Parent"]["$dtId"];
                }
                else
                {
                    log.LogInformation($"*** No parent found");
                    return null;
                }
            }
            catch (ErrorResponseException exc)
            {
                log.LogInformation($"*** Error in retrieving parent:{exc.Response.StatusCode}");
            }
            return null;
        }

        public static async Task UpdateTwinProperty(string twinId, string operation, string propertyPath, string schema, string value, ILogger log)
        {
            // Update twin property
            try
            {
                List<object> twinData = new List<object>();
                twinData.Add(new Dictionary<string, object>() {
                    { "op", operation},
                    { "path", propertyPath},
                    { "value", ConvertStringToType(schema, value)}
                });

                await client.DigitalTwins.UpdateAsync(twinId, twinData);
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

        private static object ConvertStringToType(string schema, string val)
        {
            switch (schema)
            {
                case "bool":
                    return bool.Parse(val);
                case "double":
                    return double.Parse(val);
                case "integer":
                    return Int32.Parse(val);
                case "datetime":
                    return DateTime.Parse(val);
                case "duration":
                    return Int32.Parse(val);
                case "string":
                default:
                    return val;
            }
        }
    }
}
