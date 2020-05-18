using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SampleFunctionApp
{
    public class DigitalTwin
    {
        [JsonPropertyName("$dtId")]
        public string Id { get; set; }

        /// <summary>
        /// Additional properties defined in the model.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> CustomProperties { get; } = new Dictionary<string, object>();
    }
}