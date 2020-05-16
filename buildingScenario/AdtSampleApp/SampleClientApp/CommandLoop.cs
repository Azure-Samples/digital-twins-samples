using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
/*using Newtonsoft.Json;
using Newtonsoft.Json.Linq;*/

using Azure.DigitalTwins.Core;
using Azure.DigitalTwins.Core.Serialization;
using Azure.DigitalTwins.Core.Models;
using Azure;
using System.Text;
using System.Text.Json;

namespace SampleClientApp
{
    public class CommandLoop
    {
        private DigitalTwinsClient client;
        public CommandLoop(DigitalTwinsClient _client)
        {
            client = _client;
            CliInitialize();
        }

        /// <summary>
        /// Uploads a model from a DTDL interface (often a JSON file)
        /// </summary>
        public async Task CommandCreateModels(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Log.Error("Please supply at least one model file name to upload to the service");
                return;
            }
            string[] modelArray = cmd.Skip(1).ToArray();
            string filename;
            string[] filenameArray = new string[modelArray.Length];
            for (int i = 0; i < filenameArray.Length; i++)
            {
                filenameArray[i] = !(modelArray[i].EndsWith(".json") | modelArray[i].EndsWith(".dtdl")) ? $"{modelArray[i]}.json" : modelArray[i];
            }
            string consoleAppDir = Path.Combine(Directory.GetCurrentDirectory(), @"Models");
            Log.Alert($"Reading from {consoleAppDir}");
            Log.Alert(string.Format("Submitting models: {0}...", string.Join(", ", filenameArray)));
            try
            {
                List<string> dtdlList = new List<string>();
                for (int i = 0; i < filenameArray.Length; i++)
                {
                    filename = Path.Combine(consoleAppDir, filenameArray[i]);
                    StreamReader r = new StreamReader(filename);
                    string dtdl = r.ReadToEnd();
                    r.Close();
                    dtdlList.Add(dtdl);
                }
                Response<IReadOnlyList<ModelData>> res = await client.CreateModelsAsync(dtdlList);
                Log.Ok($"Model(s) created successfully!");
                foreach (ModelData md in res.Value)
                    LogResponse(md.Model);
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Response {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Decommission a model (disable it from being created)
        /// </summary>
        public async Task CommandDecommissionModel(string[] cmd)
        {
            if (cmd.Length != 2)
            {
                Log.Error("Please supply a single model id for the model to decommission");
                return;
            }
            string model_id = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                await client.DecommissionModelAsync(model_id);
                Log.Ok($"Model decommissioned successfully!");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Gets all models
        /// </summary>
        public async Task CommandGetModels(string[] cmd)
        {
            bool include_model_definition = false;
            string[] dependencies_for = null;
            if (cmd.Length > 1)
            {
                try
                {
                    include_model_definition = bool.Parse(cmd[1]);
                }
                catch (Exception e)
                {
                    Log.Error("If you specify more than one parameter, your second parameter needs to be a boolean (return full model yes/no)");
                }
            }
            if (cmd.Length > 2)
            {
                dependencies_for = cmd.Skip(2).ToArray();
            }
            Log.Alert($"Submitting...");
            try
            {
                List<ModelData> reslist = new List<ModelData>();
                AsyncPageable<ModelData> results = client.GetModelsAsync(dependencies_for, include_model_definition);
                await foreach (ModelData md in results)
                {
                    Log.Out(md.Id);
                    if (md.Model != null)
                        LogResponse(md.Model);
                    reslist.Add(md);
                }
                Log.Out("");
                Log.Alert($"Found {reslist.Count} model(s)");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets model based on specified Id
        /// </summary>
        public async Task CommandGetModel(string[] cmd)
        {
            if (cmd.Length != 2)
            {
                Log.Error("Please supply a single model id to retrieve");
                return;
            }
            string model_id = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                Response<ModelData> res = await client.GetModelAsync(model_id);
                LogResponse(res.Value.Model);
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a model based on specified Id
        /// </summary>
        public async Task CommandDeleteModel(string[] cmd)
        {
            if (cmd.Length != 2)
            {
                Log.Error("Please supply a single model id to delete");
                return;
            }
            string model_id = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                //Response<ModelData> res = await client.DeleteModelAsync(model_id);
                //LogResponse(res.Value.Model);
                await client.DeleteModelAsync(model_id);
                Log.Ok("Model deleted successfully");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Query your digital twins graph
        /// </summary>
        public async Task CommandQuery(string[] cmd)
        {
            string query = "SELECT * FROM DIGITALTWINS";
            if (cmd.Length > 1)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 1; i < cmd.Length; i++)
                    sb.Append(cmd[i] + " ");
                query = sb.ToString();
            }
            Log.Alert($"Submitting query: {query}...");
            List<string> reslist = await Query(query);
            foreach (string item in reslist)
                LogResponse(item);
            Log.Out("End Query");
        }

        private async Task<List<string>> Query(string query)
        {
            try
            {
                AsyncPageable<string> qresult = client.QueryAsync(query);
                List<string> reslist = new List<string>();
                await foreach (string item in qresult)
                    reslist.Add(item);
                return reslist;
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Create a twin with the specified properties
        /// </summary>
        public async Task CommandCreateDigitalTwin(string[] cmd)
        {
            Log.Alert($"Preparing...");
            if (cmd.Length < 2)
            {
                Log.Error("Please specify a model id as the first argument");
                return;
            }
            string model_id = cmd[1];
            string twin_id = Guid.NewGuid().ToString();
            if (cmd.Length > 2)
                twin_id = cmd[2];
            string[] args = cmd.Skip(3).ToArray();

            Dictionary<string, object> meta = new Dictionary<string, object>()
            {
                { "$model", model_id},
                { "$kind", "DigitalTwin" }
            };
            Dictionary<string, object> twinData = new Dictionary<string, object>()
            {
                { "$metadata", meta },
            };
            for (int i = 0; i < args.Length; i += 3)
            {
                twinData.Add(args[i], convertStringToType(args[i + 1], args[i + 2]));
            }
            Log.Alert($"Submitting...");

            try
            {
                await client.CreateDigitalTwinAsync(twin_id, JsonSerializer.Serialize(twinData));
                Log.Ok($"Twin '{twin_id}' created successfully!");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Delete a twin with the specified id 
        /// </summary>
        public async Task CommandDeleteDigitalTwin(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Log.Error("Please specify the id of the twin you wish to delete");
                return;
            }

            string twin_id = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                await client.DeleteDigitalTwinAsync(twin_id);
                Log.Ok($"Twin '{twin_id}' deleted successfully!");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a twin with the specified id 
        /// </summary>
        public async Task CommandGetDigitalTwin(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Log.Error("Please specify the id of the twin you wish to retrieve");
                return;
            }

            string twin_id = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                Response<string> res = await client.GetDigitalTwinAsync(twin_id);
                if (res != null)
                    LogResponse(res.Value);
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }





        /// <summary>
        /// Update specific properties of a twin
        /// </summary>
        public async Task CommandUpdateDigitalTwin(string[] cmd)
        {
            if (cmd.Length < 6)
            {
                Log.Error("Please specify a twin id and at least one set of patch operations (op|path|schema|value)");
                return;
            }
            string twin_id = cmd[1];
            string[] args = cmd.Skip(2).ToArray();
            if (args.Length % 4 != 0)
            {
                Log.Error("Incomplete operation info. Each operation needs 4 parameters: op|path|schema|value");
                return;
            }

            List<object> twinData = new List<object>();
            for (int i = 0; i < args.Length; i += 4)
            {
                twinData.Add(new Dictionary<string, object>() {
                    { "op", args[i]},
                    { "path", args[i + 1]},
                    { "value", convertStringToType(args[i + 2], args[i + 3])}
                });
            }
            Log.Alert($"Submitting...");
            try
            {
                await client.UpdateDigitalTwinAsync(twin_id, JsonSerializer.Serialize(twinData));
                Log.Ok($"Twin '{twin_id}' updated successfully!");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Create a Relationship between a source twin and target twin with the specified relationship name
        /// </summary>
        public async Task CommandCreateRelationship(string[] cmd)
        {
            if (cmd.Length < 5)
            {
                Log.Error("To create an Relationship you must specify at least source twin, target twin, relationship name and relationship id");
                return;
            }
            string source_twin_id = cmd[1];
            string relationship_name = cmd[2];
            string target_twin_id = cmd[3];
            string relationship_id = cmd[4];

            string[] args = null;
            if (cmd.Length > 5)
            {
                args = cmd.Skip(5).ToArray();
                if (args.Length % 3 != 0)
                {
                    Log.Error("To add properties to relationships specify triples of propName schema value");
                    return;
                }
            }

            Dictionary<string, object> body = new Dictionary<string, object>()
            {
                { "$targetId", target_twin_id},
                { "$relationshipName", relationship_name}
            };
            if (args != null)
            {
                for (int i = 0; i < args.Length; i += 3)
                {
                    body.Add(args[i], convertStringToType(args[i + 1], args[i + 2]));
                }
            }
            Log.Out($"Submitting...");
            try
            {
                await client.CreateRelationshipAsync(source_twin_id, relationship_id, JsonSerializer.Serialize(body));
                Log.Ok($"Relationship {relationship_id} of type {relationship_name} created successfully from {source_twin_id} to {target_twin_id}!");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Delete a relationship from a source twin with the specified relationship name and relationship id
        /// </summary>
        public async Task CommandDeleteRelationship(string[] cmd)
        {
            if (cmd.Length < 4)
            {
                Log.Error("To delete a relationship you must specify the twin id, relationship name and relationship id");
                return;
            }
            string source_twin_id = cmd[1];
            string relationship_name = cmd[2];
            string relationship_id = cmd[3];
            Log.Alert($"Submitting...");
            try
            {
                await client.DeleteRelationshipAsync(source_twin_id, relationship_id);
                Log.Ok($"Relationship '{relationship_id}' for twin '{source_twin_id}' of type '{relationship_name}' deleted successfully!");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Get a relationship with the specified source twin
        /// </summary>
        public async Task CommandGetRelationships(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Log.Error("To list relationships you must specify the twin id");
                return;
            }
            string source_twin_id = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                AsyncPageable<string> res = client.GetRelationshipsAsync(source_twin_id);
                await foreach (string s in res)
                {
                    LogResponse(s);
                }
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Get a relationship with a specified source twin, relationship name and relationship id
        /// </summary>
        public async Task CommandGetRelationship(string[] cmd)
        {
            if (cmd.Length < 4)
            {
                Log.Error("To retrieve a relationship you must specify the twin id, relationship name and relationship id");
                return;
            }

            string source_twin_id = cmd[1];
            string relationship_name = cmd[2];
            string relationship_id = cmd[3];
            Log.Alert($"Submitting...");
            try
            {
                Response<string> res = await client.GetRelationshipAsync(source_twin_id, relationship_id);
                if (res != null)
                    LogResponse(res.Value);
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        public async Task CommandGetIncomingRelationships(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Log.Error("To list incoming relationships you must specify the twin id");
                return;
            }
            string source_twin_id = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                AsyncPageable<IncomingRelationship> res = client.GetIncomingRelationshipsAsync(source_twin_id);
                await foreach (IncomingRelationship ie in res)
                {
                    Log.Ok($"Relationship: {ie.RelationshipName} from {ie.SourceId} | {ie.RelationshipId}");
                }
                Log.Out("--Completed--");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Create an event route with a specified id
        /// </summary>
        public async Task CommandCreateEventRoute(string[] cmd)
        {
            if (cmd.Length < 4)
            {
                Log.Error("To create an event route you must specify the route id, the endpoint id and a filter");
                return;
            }

            string route_id = cmd[1];
            EventRoute er = new EventRoute(cmd[2]);

            StringBuilder sb = new StringBuilder();
            for (int i = 3; i < cmd.Length; i++)
                sb.Append(cmd[i] + " ");
            er.Filter = sb.ToString();
            Log.Alert($"Submitting...");
            try
            {
                await client.CreateEventRouteAsync(route_id, er);
                Log.Ok("Command completed");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Get an event route with a specified id
        /// </summary>
        public async Task CommandGetEventRoute(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Log.Error("To retrieve an event route you must specify the route id");
                return;
            }

            string route_id = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                Response<EventRoute> res = await client.GetEventRouteAsync(route_id);
                if (res != null && res.Value!=null)
                {
                    Log.Out($"Route {res.Value.Id} to {res.Value.EndpointId}");
                    Log.Out($"  Filter: {res.Value.Filter}");
                }
                    
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Get all event routes
        /// </summary>
        public async Task CommandGetEventRoutes(string[] cmd)
        {
            Log.Alert($"Submitting...");
            try
            {
                AsyncPageable<EventRoute> res = client.GetEventRoutesAsync();
                await foreach(EventRoute er in res)
                {
                    Log.Out($"Route {er.Id} to {er.EndpointId}");
                    Log.Out($"  Filter: {er.Filter}");
                }
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        /// <summary>
        /// Delete an event route with a specified id
        /// </summary>
        public async Task CommandDeleteEventRoute(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Log.Error("To delete an event route you must specify the route id");
                return;
            }

            string route_id = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                await client.DeleteEventRouteAsync(route_id);
                Log.Ok("Command completed");
            }
            catch (RequestFailedException e)
            {
                Log.Error($"Error {e.Status}: {e.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error: {ex}");
            }
        }

        public async Task FindAndDeleteOutgoingRelationshipsAsync(string dtId)
        {
            // Find the relationships for the twin

            try
            {
                // GetRelationshipsAsync will throw if an error occurs
                AsyncPageable<string> relsJson = client.GetRelationshipsAsync(dtId);

                await foreach (string relJson in relsJson)
                {
                    var rel = System.Text.Json.JsonSerializer.Deserialize<BasicRelationship>(relJson);
                    await client.DeleteRelationshipAsync(dtId, rel.Id).ConfigureAwait(false);
                    Log.Ok($"Deleted relationship {rel.Id} from {dtId}");
                }
            }
            catch (RequestFailedException ex)
            {
                Log.Error($"*** Error {ex.Status}/{ex.ErrorCode} retrieving or deleting relationships for {dtId} due to {ex.Message}");
            }
        }

        async Task FindAndDeleteIncomingRelationshipsAsync(string dtId)
        {
            // Find the relationships for the twin

            try
            {
                // GetRelationshipssAsync will throw if an error occurs
                AsyncPageable<IncomingRelationship> incomingRels = client.GetIncomingRelationshipsAsync(dtId);

                await foreach (IncomingRelationship incomingRel in incomingRels)
                {
                    await client.DeleteRelationshipAsync(incomingRel.SourceId, incomingRel.RelationshipId).ConfigureAwait(false);
                    Log.Ok($"Deleted incoming relationship {incomingRel.RelationshipId} from {dtId}");
                }
            }
            catch (RequestFailedException ex)
            {
                Log.Error($"*** Error {ex.Status}/{ex.ErrorCode} retrieving or deleting incoming relationships for {dtId} due to {ex.Message}");
            }
        }

        public async Task DeleteAllTwinsAsync()
        {
            Log.Alert($"\nDeleting all twins");
            Log.Out($"Step 1: Find all twins", ConsoleColor.DarkYellow);
            List<string> twinlist = new List<string>();
            try
            {
                AsyncPageable<string> qresult = client.QueryAsync("SELECT * FROM DIGITALTWINS");
                await foreach (string item in qresult)
                {
                    JsonDocument document = JsonDocument.Parse(item);
                    if (document.RootElement.TryGetProperty("$dtId", out JsonElement eDtdl))
                    {
                        try
                        {
                            string twinId = eDtdl.GetString();
                            twinlist.Add(twinId);
                        } catch (Exception e)
                        {
                            Log.Error("No DTDL property in query result");
                        }
                    }
                    else
                    {
                        Log.Error($"Error: Can't find twin id in query result:\n {item}");
                    }
                }       
            } catch (Exception ex)
            {
                Log.Error($"Error in query execution: {ex.Message}");
            }
            

            Log.Out($"Step 2: Find and remove relationships for each twin...", ConsoleColor.DarkYellow);
            foreach (string twinId in twinlist)
            {         
                    // Remove any relationships for the twin
                    await FindAndDeleteOutgoingRelationshipsAsync(twinId).ConfigureAwait(false);
                    await FindAndDeleteIncomingRelationshipsAsync(twinId).ConfigureAwait(false);
            }

            Log.Out($"Step 3: Delete all twins", ConsoleColor.DarkYellow);
            foreach (string twinId in twinlist)
            {
                try
                {
                    await client.DeleteDigitalTwinAsync(twinId).ConfigureAwait(false);
                    Log.Out($"Deleted twin {twinId}");
                }
                catch (RequestFailedException ex)
                {
                    Log.Error($"*** Error {ex.Status}/{ex.ErrorCode} deleting twin {twinId} due to {ex.Message}");
                }
            }
        }

        public async Task CommandDeleteAllTwins(string[] args)
        {
            await DeleteAllTwinsAsync();
        }

        public async Task CommandDeleteAllModels(string[] args)
        {
            Log.Error("Not implemented yet");
        }

        /// <summary>
        /// Create some twins to represent a building
        /// </summary>
        public async Task CommandSetupBuildingScenario(string[] cmd)
        {
            Log.Out($"Initializing Building Scenario...");
            BuildingScenario b = new BuildingScenario(this);
            await b.InitBuilding();
        }

        /// <summary>
        /// Get a twin with the specified id in a cycle
        /// </summary>
        /// <param name='twin_id0'>
        /// Id of an existing twin
        /// </param>
        public async Task CommandObserveProperties(string[] cmd)
        {
            if (cmd.Length < 3)
            {
                Log.Error("Please provide at least one pair of twin-id and property name to observe");
                return;
            }
            string[] args = cmd.Skip(1).ToArray();
            if (args.Length % 2 != 0 || args.Length > 8)
            {
                Log.Error("Please provide pairs of twin-id and property names (up to 4) to observe");
                return;
            }
            Log.Alert($"Starting observation...");
            TimerState state = new TimerState();
            state.Arguments = args;
            var stateTimer = new System.Threading.Timer(CheckState, state, 0, 2000);
            Log.Alert("Press any key to end observation");
            Console.ReadKey(true);
            state.Active = false;
            stateTimer.Dispose();
        }

        private class TimerState
        {
            public bool Active = true;
            public string[] Arguments;
        }

        private void CheckState(object state)
        {
            TimerState ts = state as TimerState;
            if (ts == null || ts.Active == false)
                return;

            for (int i = 0; i < ts.Arguments.Length; i += 2)
            {
                try
                {
                    Response<string> res0 = client.GetDigitalTwin(ts.Arguments[i]);
                    if (res0 != null)
                        LogProperty(res0.Value, ts.Arguments[i + 1]);
                }
                catch (RequestFailedException e)
                {
                    Log.Error($"Error {e.Status}: {e.Message}");
                }
                catch (Exception ex)
                {
                    Log.Error($"Error: {ex}");
                }
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///
        /// Helper Functions
        ///
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        //Cast a string to its intended type
        public object convertStringToType(string schema, string val)
        {
            switch (schema)
            {
                case "boolean":
                    return bool.Parse(val);
                case "double":
                    return double.Parse(val);
                case "integer":
                case "int":
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

        // Log a JSON serialized object to the command prompt
        public void LogResponse(string res, string type = "")
        {
            if (type != "")
                Log.Alert($"{type}: \n");
            else
                Log.Alert("Response:");
            if (res == null)
                Log.Out("Null response");
            else
            {
                //string res_json = JsonConvert.SerializeObject(res, Formatting.Indented);
                Console.WriteLine(PrettifyJson(res));
            }
        }

        //Log temperature changes in sample app
        public void LogProperty(string res, string propName = "Temperature")
        {
            Dictionary<string, object> obj = JsonSerializer.Deserialize<Dictionary<string, object>>(res);
            object dtid;
            if (obj.TryGetValue("$dtId", out dtid) == false)
                dtid = "<$dtId not found>";
            object value;
            if (obj.TryGetValue(propName, out value) == false)
                value = "<property not found>";
            Console.WriteLine($"$dtId: {dtid}, {propName}: {value}");
        }

        private string PrettifyJson(string json)
        {
            object jsonObj = System.Text.Json.JsonSerializer.Deserialize<object>(json);
            return System.Text.Json.JsonSerializer.Serialize(jsonObj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///
        /// Console UI
        ///
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private struct CliInfo
        {
            public string Help;
            public Func<string[], Task> Command;
            public CliCategory Category;
        }

        private enum CliCategory
        {
            ADTModels,
            ADTTwins,
            ADTQuery,
            ADTRoutes,
            SampleScenario,
            SampleTools
        }

        Dictionary<string, CliInfo> commands;
        private void CliInitialize()
        {
            commands = new Dictionary<string, CliInfo> {
                { "Help", new CliInfo { Command=CommandHelp, Category = CliCategory.SampleTools, Help="List all commands" } },
                { "CreateModels", new CliInfo { Command=CommandCreateModels, Category = CliCategory.ADTModels, Help="<model-filename-0> <model-filename-1> ..." } },
                { "GetModels", new CliInfo { Command=CommandGetModels, Category = CliCategory.ADTModels, Help="[true] option to include full model definition [model-id]... optional model-ids to get dependencies for" } },
                { "GetModel", new CliInfo { Command=CommandGetModel, Category = CliCategory.ADTModels, Help = "<model-id>" } },
                { "DecommissionModel", new CliInfo { Command=CommandDecommissionModel, Category = CliCategory.ADTModels, Help="<model-id> " } },
                { "DeleteModel", new CliInfo { Command=CommandDeleteModel, Category = CliCategory.ADTModels, Help="<model-id>" } },
                { "Query", new CliInfo { Command=CommandQuery, Category = CliCategory.ADTQuery, Help="[query-string] (default query is 'SELECT * FROM DIGITAL TWINS')" } },
                { "CreateDigitalTwin", new CliInfo { Command=CommandCreateDigitalTwin, Category = CliCategory.ADTTwins, Help="<model-id> <twin-id> <property-name-0> <prop-type-0> <prop-value-0> ..." } },
                { "UpdateDigitalTwin", new CliInfo { Command=CommandUpdateDigitalTwin, Category = CliCategory.ADTTwins, Help="<twin-id> <operation-0> <path-0> <value-schema-0> <value-0> ..." } },
                { "GetDigitalTwin", new CliInfo { Command=CommandGetDigitalTwin, Category = CliCategory.ADTTwins, Help="<twin-id>" } },
                { "DeleteDigitalTwin", new CliInfo { Command=CommandDeleteDigitalTwin, Category = CliCategory.ADTTwins, Help="<twin-id>" } },
                { "CreateRelationship", new CliInfo { Command=CommandCreateRelationship, Category = CliCategory.ADTTwins, Help="<source-twin-id> <relationship-name> <target-twin-id> <relationship-id> <property-name-0> <prop-type-0> <prop-value-0> ..." } },
                { "DeleteRelationship", new CliInfo { Command=CommandDeleteRelationship, Category = CliCategory.ADTTwins, Help="<source-twin-id> <relationship-name> <relationship-id>" } },
                { "GetRelationships", new CliInfo { Command=CommandGetRelationships, Category = CliCategory.ADTTwins, Help="twin-id" } },
                { "GetRelationship", new CliInfo { Command=CommandGetRelationship, Category = CliCategory.ADTTwins, Help="<source-twin-id> <relationship-name> <relationship-id>" } },
                { "GetIncomingRelationships", new CliInfo { Command=CommandGetIncomingRelationships, Category = CliCategory.ADTTwins, Help="<source-twin-id>" } },
                { "CreateEventRoute", new CliInfo { Command=CommandCreateEventRoute, Category = CliCategory.ADTRoutes, Help="<route-id> <endpoint-id> <filter>" } },
                { "GetEventRoute", new CliInfo { Command=CommandGetEventRoute, Category = CliCategory.ADTRoutes, Help="<route-id>" } },
                { "GetEventRoutes", new CliInfo { Command=CommandGetEventRoutes, Category = CliCategory.ADTRoutes, Help="" } },
                { "DeleteEventRoute", new CliInfo { Command=CommandDeleteEventRoute, Category = CliCategory.ADTRoutes, Help="<route-id>" } },
                { "SetupBuildingScenario", new CliInfo { Command=CommandSetupBuildingScenario, Category = CliCategory.SampleScenario, Help="loads a set of models and creates a very simple example twins graph" } },
                { "ObserveProperties", new CliInfo { Command=CommandObserveProperties, Category = CliCategory.SampleScenario, Help="<twin id> <propertyName> <twin-id> <property name>... observes the selected properties on the selected twins" } },
                { "DeleteAllTwins", new CliInfo { Command=CommandDeleteAllTwins, Category = CliCategory.SampleTools, Help="Deletes all the twins in your instance" } },
                { "DeleteAllModels", new CliInfo { Command=CommandDeleteAllModels, Category = CliCategory.SampleTools, Help="Deletes all models in your instance" } },
                { "Exit", new CliInfo { Command=CommandExit, Category = CliCategory.SampleTools, Help="Exits the program" } },
            };
        }

        public async Task CommandExit(string[] args = null)
        {
            Environment.Exit(0);
        }

        public async Task CommandHelp(string[] args = null)
        {
            Log.Ok("This sample app lets you construct a simple digital twins graph");
            Log.Ok("and issue some commands against the ADT service instance");
            Log.Alert("*** See the command implementation for usage examples of the ADT C# SDK");
            Log.Out("See the sample documentation for instruction on how to set up additional services");
            Log.Out("to run an end-to-end demo");
            Log.Out("");
            if (args != null && args.Length < 2)
            {
                Log.Alert("Scenario Demo Commands:");
                CliPrintCategoryCommands(CliCategory.SampleScenario);
                Log.Alert("Some ADT Commands for Model Management:");
                CliPrintCategoryCommands(CliCategory.ADTModels);
                Log.Alert("Some ADT Commands for Twins:");
                CliPrintCategoryCommands(CliCategory.ADTTwins);
                Log.Alert("ADT Commands for Query:");
                CliPrintCategoryCommands(CliCategory.ADTQuery);
                Log.Alert("ADT Commands for Event Routes:");
                CliPrintCategoryCommands(CliCategory.ADTRoutes);
                Log.Alert("Others:");
                CliPrintCategoryCommands(CliCategory.SampleTools);
            }
        }

        private void CliPrintCategoryCommands(CliCategory cat)
        {
            var clist = commands.Where(p => p.Value.Category == cat).Select(p => new { Cmd = p.Key, Help = p.Value.Help });
            foreach (var item in clist)
            {
                Log.Out($"  {item.Cmd} {item.Help}");
            }
        }

        public async Task CliCommandInterpreter()
        {
            Log.Out("");
            await CommandHelp(new string[] { "help", "headerOnly" });
            Log.Out("");
            while (true)
            {
                try
                {
                    Log.Alert("\nPlease enter a command or 'help'. Commands are not case sensitive");
                    string command = Console.ReadLine().Trim();
                    string[] commandArr = command.Split(null);
                    string verb = commandArr[0].ToLower();
                    if (verb != null && verb != "")
                    {
                        var cmd = commands
                                    .Where(p => p.Key.ToLower() == verb)
                                    .Select(p => p.Value.Command)
                                    .Single();
                        await cmd(commandArr);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Invalid command. Please type 'help' for more information.");
                }
            }
        }

    }
}
