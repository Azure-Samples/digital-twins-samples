using Azure;
using Azure.DigitalTwins.Core;
using Azure.DigitalTwins.Core.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace SampleFunctionsApp
{
    internal static class AdtUtilities
    {
        public static async Task<string> FindParentAsync(DigitalTwinsClient client, string child, string relname, ILogger log)
        {
            // Find parent using incoming relationships
            try
            {
                AsyncPageable<IncomingRelationship> rels = client.GetIncomingRelationshipsAsync(child);

                await foreach (IncomingRelationship ie in rels)
                {
                    if (ie.RelationshipName == relname)
                        return (ie.SourceId);
                }
            }
            catch (RequestFailedException exc)
            {
                log.LogInformation($"*** Error in retrieving parent:{exc.Status}:{exc.Message}");
            }
            return null;
        }

        public static async Task<string> FindParentByQueryAsync(DigitalTwinsClient client, string childId, ILogger log)
        {
            string query = "SELECT Parent " +
                           "FROM digitaltwins Parent " +
                           "JOIN Child RELATED Parent.contains " +
                           $"WHERE Child.$dtId = '{childId}'";
            log.LogInformation($"Query: {query}");

            try
            {
                AsyncPageable<string> res = client.QueryAsync(query);

                await foreach (string s in res)
                {
                    JObject parentTwin = (JObject)JsonConvert.DeserializeObject(s);
                    return (string)parentTwin["Parent"]["$dtId"];
                }
                log.LogWarning($"*** No parent found");
            }
            catch (RequestFailedException exc)
            {
                log.LogError($"*** Error in retrieving parent:{exc.Status}/{exc.Message}");
            }
            return null;
        }

        public static async Task UpdateTwinPropertyAsync(DigitalTwinsClient client, string twinId, string propertyPath, object value, ILogger log)
        {
            // If the twin does not exist, this will log an error
            try
            {
                var updateTwinData = new JsonPatchDocument();
                updateTwinData.AppendAdd(propertyPath, value);

                log.LogInformation($"UpdateTwinPropertyAsync sending {updateTwinData}");
                await client.UpdateDigitalTwinAsync(twinId, updateTwinData);
            }
            catch (RequestFailedException exc)
            {
                log.LogInformation($"*** Error:{exc.Status}/{exc.Message}");
            }
        }
    }
}
