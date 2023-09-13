using Azure;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Azure.Messaging.EventGrid;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SampleFunctionsApp
{
    // This class processes telemetry events from IoT Hub, reads temperature of a device
    // and sets the "Temperature" property of the device with the value of the telemetry.
    public class ProcessHubToDTEvents
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static string adtServiceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        private readonly ILogger _logger;

        public ProcessHubToDTEvents(ILogger<ProcessDTRoutedData> logger)
        {
            _logger = logger;
        }

        [Function("ProcessHubToDTEvents")]
        public async Task Run([EventGridTrigger]EventGridEvent eventGridEvent)
        {
            // After this is deployed, you need to turn the Managed Identity Status to "On",
            // Grab Object Id of the function and assigned "Azure Digital Twins Owner (Preview)" role
            // to this function identity in order for this function to be authorized on ADT APIs.

            //Authenticate with Digital Twins
            var credentials = new DefaultAzureCredential();
            DigitalTwinsClient client = new DigitalTwinsClient(
                new Uri(adtServiceUrl), credentials, new DigitalTwinsClientOptions
                { Transport = new HttpClientTransport(httpClient) });
            _logger.LogInformation($"ADT service client connection created.");

            if (eventGridEvent != null && eventGridEvent.Data != null)
            {
                _logger.LogInformation(eventGridEvent.Data.ToString());

                // Reading deviceId and temperature for IoT Hub JSON
                JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
                string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];
                var temperature = deviceMessage["body"]["Temperature"];

                _logger.LogInformation($"Device:{deviceId} Temperature is:{temperature}");

                //Update twin using device temperature
                var updateTwinData = new JsonPatchDocument();
                updateTwinData.AppendReplace("/Temperature", temperature.Value<double>());
                await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);
            }
        }
    }
}