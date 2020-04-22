using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ADTApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DigitalTwinsSample
{
    public class BuildingScenario : IScenario
    {
        private CommandLoop cl;
        public BuildingScenario(CommandLoop cl_)
        {
            cl = cl_;
        }

        async Task IScenario.init()
        {
            cl.Log($"Resetting instance...");
            await resetInstance();
            cl.Log($"Creating 1 floor, 1 room and 1 thermostat...");
            await initializeGraph();
        }

        private async Task resetInstance()
        {
            try
            {
                // Query for all twins
                List<QueryResult> allTwins = await cl.SubmitQueryTwins("Select * from DigitalTwins");
                List<Dictionary<string, object>> allTwins_casted = new List<Dictionary<string, object>>();
                foreach (QueryResult qr in allTwins)
                {
                    foreach (object o in qr.Items)
                    {
                        var obj = JObject.FromObject(o).ToObject<Dictionary<string, object>>();
                        allTwins_casted.Add((Dictionary<string, object>)obj);
                    }
                }

                // Delete all edges
                foreach (Dictionary<string, object> currTwin in allTwins_casted)
                {
                    EdgeCollection currTwinEdges = await cl.SubmitListEdges((string)currTwin["$dtId"]);
                    foreach (object currEdge_uncasted in currTwinEdges.Value)
                    {
                        var currEdge = JObject.FromObject(currEdge_uncasted).ToObject<Dictionary<string, object>>();
                        await cl.SubmitDeleteEdge((string)currTwin["$dtId"], (string)currEdge["$relationship"], (string)currEdge["$edgeId"]);
                    }
                }

                // Delete all twins
                foreach (Dictionary<string, object> currTwin in allTwins_casted)
                {
                    await cl.SubmitDeleteTwin((string)currTwin["$dtId"]);
                }
            }
            catch (Exception ex)
            {
                cl.Log($"Error: {ex.Message}");
            }
        }

        private async Task initializeGraph()
        {
            string[] models_to_upload = new string[2] { "ThermostatModel", "SpaceModel" };
            cl.Log($"Uploading {string.Join(", ", models_to_upload)} models");
            try
            {
                await cl.SubmitAddModels(models_to_upload);
            }
            catch (ErrorResponseException ex)
            {
                cl.Log($"Error: {ex.Message}");
            }

            try
            {
                cl.Log($"Creating SpaceModel and Thermostat...");
                await cl.SubmitAddTwin("urn:contosocom:DigitalTwins:Space:1", new string[12]
                {
            "DisplayName", "string", "Floor 1",
            "Location", "string", "Puget Sound",
            "Temperature", "double", "0",
            "ComfortIndex", "double", "0"
                }, "floor1");
                await cl.SubmitAddTwin("urn:contosocom:DigitalTwins:Space:1", new string[12]
                {
            "DisplayName", "string", "Room 21",
            "Location", "string", "Puget Sound",
            "Temperature", "double", "0",
            "ComfortIndex", "double", "0"
                }, "room21");
                await cl.SubmitAddTwin("urn:contosocom:DigitalTwins:Thermostat:1", new string[15]
                {
            "DisplayName", "string", "Thermostat 67",
            "Location", "string", "Puget Sound",
            "FirmwareVersion", "string", "1.3.9",
            "Temperature", "double", "0",
            "ComfortIndex", "double", "0"
                }, "thermostat67");

                cl.Log($"Creating edges between the Floor, Room and Thermostat");
                await cl.SubmitAddEdge("floor1", "contains", "room21", new string[6]
                {
            "ownershipUser", "string", "Contoso",
            "ownershipDepartment", "string", "Comms Division"
                }, "floor_to_room_edge");
                await cl.SubmitAddEdge("room21", "contains", "thermostat67", new string[6]
                {
            "ownershipUser", "string", "Contoso",
            "ownershipDepartment", "string", "Comms Division"
                }, "room_to_therm_edge");
            }
            catch (ErrorResponseException ex)
            {
                cl.Log($"Error: {ex.Message}");
            }
        }
    }
}
