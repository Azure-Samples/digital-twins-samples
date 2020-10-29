using Azure;
using Azure.DigitalTwins.Core;
using Azure.DigitalTwins.Core.Serialization;
using Microsoft.Azure.DigitalTwins.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SampleClientApp
{
    public class CommandLoop
    {
        private readonly DigitalTwinsClient client;

        public CommandLoop(DigitalTwinsClient client)
        {
            this.client = client;
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
                Response<ModelData[]> res = await client.CreateModelsAsync(dtdlList);
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
            string modelId = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                await client.DecommissionModelAsync(modelId);
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
            bool includeModelDefinitions = false;
            string[] dependenciesFor = null;
            if (cmd.Length > 1)
            {
                try
                {
                    includeModelDefinitions = bool.Parse(cmd[1]);
                }
                catch (Exception)
                {
                    Log.Error("If you specify more than one parameter, your second parameter needs to be a boolean (return full model yes/no)");
                }
            }
            if (cmd.Length > 2)
            {
                dependenciesFor = cmd.Skip(2).ToArray();
            }
            Log.Alert($"Submitting...");
            try
            {
                var reslist = new List<ModelData>();
                AsyncPageable<ModelData> results = client.GetModelsAsync(dependenciesFor, includeModelDefinitions);
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
            string modelId = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                Response<ModelData> res = await client.GetModelAsync(modelId);
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
            string modelId = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                await client.DeleteModelAsync(modelId);
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
        /// Delete all models
        /// </summary>
        public async Task CommandDeleteAllModels(string[] cmd)
        {
            Log.Alert($"Submitting...");
            try
            {
                var reslist = new List<string>();
                AsyncPageable<ModelData> results = client.GetModelsAsync(null, true);
                await foreach (ModelData md in results)
                {
                    Log.Out(md.Id);
                    if (md.Model != null)
                    {
                        Log.Out(md.Id);
                        reslist.Add(md.Model);
                    }
                }
                Log.Out("");
                Log.Alert($"Found {reslist.Count} model(s)");

                ModelParser parser = new ModelParser();
                try
                {
                    IReadOnlyDictionary<Dtmi, DTEntityInfo> om = await parser.ParseAsync(reslist);
                    Log.Ok("Models parsed successfully. Deleting models...");

                    var interfaces = new List<DTInterfaceInfo>();
                    IEnumerable<DTInterfaceInfo> ifenum = from entity in om.Values
                                                          where entity.EntityKind == DTEntityKind.Interface
                                                          select entity as DTInterfaceInfo;
                    interfaces.AddRange(ifenum);
                    int pass = 1;
                    // DeleteModels can only delete models that are not in the inheritance chain of other models
                    // or used as components by other models. Therefore, we use the model parser to parse the DTDL
                    // and then find the "leaf" models, and delete these.
                    // We repeat this process until no models are left.
                    while (interfaces.Count() > 0)
                    {
                        Log.Out($"Model deletion pass {pass++}");
                        Dictionary<Dtmi, DTInterfaceInfo> referenced = new Dictionary<Dtmi, DTInterfaceInfo>();
                        foreach (DTInterfaceInfo i in interfaces)
                        {
                            foreach (DTInterfaceInfo ext in i.Extends)
                            {
                                referenced.TryAdd(ext.Id, ext);
                            }
                            IEnumerable<DTComponentInfo> components = from content in i.Contents.Values
                                                                      where content.EntityKind == DTEntityKind.Component
                                                                      select content as DTComponentInfo;
                            foreach (DTComponentInfo comp in components)
                            {
                                referenced.TryAdd(comp.Schema.Id, comp.Schema);
                            }
                        }
                        List<DTInterfaceInfo> toDelete = new List<DTInterfaceInfo>();
                        foreach (DTInterfaceInfo iface in interfaces)
                        {
                            if (referenced.TryGetValue(iface.Id, out DTInterfaceInfo result) == false)
                            {
                                Log.Alert($"Can delete {iface.Id}");
                                toDelete.Add(iface);
                            }
                        }
                        foreach (DTInterfaceInfo del in toDelete)
                        {
                            interfaces.Remove(del);
                            try
                            {
                                await client.DeleteModelAsync(del.Id.ToString());
                                Log.Ok($"Model {del.Id} deleted successfully");
                            }
                            catch (RequestFailedException e)
                            {
                                Log.Error($"Error deleting model {e.Status}: {e.Message}");
                            }
                        }
                    }
                }
                catch (ParsingException pe)
                {
                    Log.Error($"*** Error parsing models");
                    int derrcount = 1;
                    foreach (ParsingError err in pe.Errors)
                    {
                        Log.Error($"Error {derrcount}:");
                        Log.Error($"{err.Message}");
                        Log.Error($"Primary ID: {err.PrimaryID}");
                        Log.Error($"Secondary ID: {err.SecondaryID}");
                        Log.Error($"Property: {err.Property}\n");
                        derrcount++;
                    }
                    return;
                }

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
                var sb = new StringBuilder();
                for (int i = 1; i < cmd.Length; i++)
                    sb.Append(cmd[i] + " ");
                query = sb.ToString();
            }
            Log.Alert($"Submitting query: {query}...");
            List<string> reslist = await Query(query);
            if (reslist != null)
            {
                foreach (string item in reslist)
                    LogResponse(item);
            }
            Log.Out("End Query");
        }

        private async Task<List<string>> Query(string query)
        {
            try
            {
                AsyncPageable<string> qresult = client.QueryAsync(query);
                var reslist = new List<string>();
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
            string modelId = cmd[1];
            string twinId = Guid.NewGuid().ToString();
            if (cmd.Length > 2)
                twinId = cmd[2];
            string[] args = cmd.Skip(3).ToArray();

            var twinData = new BasicDigitalTwin
            {
                Id = twinId,
                Metadata =
                {
                    ModelId = modelId,
                },
            };

            for (int i = 0; i < args.Length; i += 3)
            {
                twinData.CustomProperties.Add(args[i], ConvertStringToType(args[i + 1], args[i + 2]));
            }
            Log.Alert($"Submitting...");

            try
            {
                await client.CreateDigitalTwinAsync(twinData.Id, JsonSerializer.Serialize(twinData));
                Log.Ok($"Twin '{twinId}' created successfully!");
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

            string twinId = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                await client.DeleteDigitalTwinAsync(twinId);
                Log.Ok($"Twin '{twinId}' deleted successfully!");
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

            string twinId = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                Response<string> res = await client.GetDigitalTwinAsync(twinId);
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
            string twinId = cmd[1];
            string[] args = cmd.Skip(2).ToArray();
            if (args.Length % 4 != 0)
            {
                Log.Error("Incomplete operation info. Each operation needs 4 parameters: op|path|schema|value");
                return;
            }

            var twinData = new List<object>();
            for (int i = 0; i < args.Length; i += 4)
            {
                twinData.Add(
                    new Dictionary<string, object>
                    {
                        { "op", args[i] },
                        { "path", args[i + 1] },
                        { "value", ConvertStringToType(args[i + 2], args[i + 3]) }
                    });
            }
            Log.Alert($"Submitting...");
            try
            {
                await client.UpdateDigitalTwinAsync(twinId, JsonSerializer.Serialize(twinData));
                Log.Ok($"Twin '{twinId}' updated successfully!");
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
            string sourceTwinId = cmd[1];
            string relationshipName = cmd[2];
            string targetTwinId = cmd[3];
            string relationshipId = cmd[4];

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

            var relationship = new BasicRelationship
            {
                Id = relationshipId,
                SourceId = sourceTwinId,
                TargetId = targetTwinId,
                Name = relationshipName,
            };

            if (args != null)
            {
                for (int i = 0; i < args.Length; i += 3)
                {
                    relationship.CustomProperties.Add(args[i], ConvertStringToType(args[i + 1], args[i + 2]));
                }
            }

            Log.Out($"Submitting...");
            try
            {
                await client.CreateRelationshipAsync(sourceTwinId, relationshipId, JsonSerializer.Serialize(relationship));
                Log.Ok($"Relationship {relationshipId} of type {relationshipName} created successfully from {sourceTwinId} to {targetTwinId}!");
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
            string sourceTwinId = cmd[1];
            string relationshipName = cmd[2];
            string relationshipId = cmd[3];
            Log.Alert($"Submitting...");
            try
            {
                await client.DeleteRelationshipAsync(sourceTwinId, relationshipId);
                Log.Ok($"Relationship '{relationshipId}' for twin '{sourceTwinId}' of type '{relationshipName}' deleted successfully!");
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
            string sourceTwinId = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                AsyncPageable<string> res = client.GetRelationshipsAsync(sourceTwinId);
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
        public async Task CommandGetRelationshipAsync(string[] cmd)
        {
            if (cmd.Length < 3)
            {
                Log.Error("To retrieve a relationship you must specify the twin id, and relationship id");
                return;
            }

            string sourceTwinId = cmd[1];
            string relationshipId = cmd[2];
            Log.Alert($"Submitting...");
            try
            {
                Response<string> res = await client.GetRelationshipAsync(sourceTwinId, relationshipId);
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
            string sourceTwinId = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                AsyncPageable<IncomingRelationship> res = client.GetIncomingRelationshipsAsync(sourceTwinId);
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

            string routeId = cmd[1];
            var er = new EventRoute(cmd[2]);

            var sb = new StringBuilder();
            for (int i = 3; i < cmd.Length; i++)
                sb.Append(cmd[i] + " ");
            er.Filter = sb.ToString();
            Log.Alert($"Submitting...");
            try
            {
                await client.CreateEventRouteAsync(routeId, er);
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

            string routeId = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                Response<EventRoute> res = await client.GetEventRouteAsync(routeId);
                if (res != null && res.Value != null)
                {
                    Log.Out($"Route {res.Value.Id} to {res.Value.EndpointName}");
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
                await foreach (EventRoute er in res)
                {
                    Log.Out($"Route {er.Id} to {er.EndpointName}");
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

            string routeId = cmd[1];
            Log.Alert($"Submitting...");
            try
            {
                await client.DeleteEventRouteAsync(routeId);
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
                    var rel = JsonSerializer.Deserialize<BasicRelationship>(relJson);
                    await client.DeleteRelationshipAsync(dtId, rel.Id);
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
                    await client.DeleteRelationshipAsync(incomingRel.SourceId, incomingRel.RelationshipId);
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
            List<string> twinList = new List<string>();
            try
            {
                AsyncPageable<string> queryResult = client.QueryAsync("SELECT * FROM DIGITALTWINS");
                await foreach (string item in queryResult)
                {
                    JsonDocument document = JsonDocument.Parse(item);
                    if (document.RootElement.TryGetProperty("$dtId", out JsonElement eDtdl))
                    {
                        try
                        {
                            string twinId = eDtdl.GetString();
                            twinList.Add(twinId);
                        }
                        catch (Exception)
                        {
                            Log.Error("No DTDL property in query result");
                        }
                    }
                    else
                    {
                        Log.Error($"Error: Can't find twin id in query result:\n {item}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error in query execution: {ex.Message}");
            }

            Log.Out($"Step 2: Find and remove relationships for each twin...", ConsoleColor.DarkYellow);
            foreach (string twinId in twinList)
            {
                // Remove any relationships for the twin
                await FindAndDeleteOutgoingRelationshipsAsync(twinId);
                await FindAndDeleteIncomingRelationshipsAsync(twinId);
            }

            Log.Out($"Step 3: Delete all twins", ConsoleColor.DarkYellow);
            foreach (string twinId in twinList)
            {
                try
                {
                    await client.DeleteDigitalTwinAsync(twinId);
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

        /// <summary>
        /// Create some twins to represent a building
        /// </summary>
        public async Task CommandSetupBuildingScenario(string[] cmd)
        {
            Log.Out($"Initializing Building Scenario...");
            var b = new BuildingScenario(this);
            await b.InitBuilding();
        }

        public async Task CommandLoadModels(string[] cmd)
        {
            if (cmd.Length < 2)
            {
                Log.Error("Please provide a directory path to load models from");
                return;
            }
            string directory = cmd[1];

            string extension = "json";
            if (cmd.Length > 2)
            {
                extension = cmd[2];
            }
            bool recursive = true;
            if (cmd.Length > 3)
            {
                if (cmd[3] == "nosub")
                    recursive = false;
                else
                    Log.Error("If you pass more than two parameters, the third parameter must be 'nosub' to skip recursive load");
            }

            DirectoryInfo dinfo;
            try
            {
                dinfo = new DirectoryInfo(directory);
            }
            catch (Exception e)
            {
                Log.Error($"Error accessing the target directory '{directory}': \n{e.Message}");
                return;
            }
            Log.Alert($"Loading *.{extension} files in folder '{dinfo.FullName}'.\nRecursive is set to {recursive}\n");
            if (dinfo.Exists == false)
            {
                Log.Error($"Specified directory '{directory}' does not exist: Exiting...");
                return;
            }
            else
            {
                SearchOption searchOpt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = dinfo.EnumerateFiles($"*.{extension}", searchOpt);
                if (files.Count() == 0)
                {
                    Log.Alert("No matching files found.");
                    return;
                }
                Dictionary<FileInfo, string> modelDict = new Dictionary<FileInfo, string>();
                int count = 0;
                string lastFile = "<none>";
                try
                {
                    foreach (FileInfo fi in files)
                    {
                        string dtdl = File.ReadAllText(fi.FullName);
                        modelDict.Add(fi, dtdl);
                        lastFile = fi.FullName;
                        count++;
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Could not read files. \nLast file read: {lastFile}\nError: \n{e.Message}");
                    return;
                }
                Log.Ok($"Read {count} files from specified directory");
                int errJson = 0;
                foreach (FileInfo fi in modelDict.Keys)
                {
                    modelDict.TryGetValue(fi, out string dtdl);
                    try
                    {
                        JsonDocument.Parse(dtdl);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Invalid json found in file {fi.FullName}.\nJson parser error \n{e.Message}");
                        errJson++;
                    }
                }
                if (errJson > 0)
                {
                    Log.Error($"\nFound  {errJson} Json parsing errors");
                    return;
                }
                Log.Ok($"Validated JSON for all files - now validating DTDL");
                var modelList = modelDict.Values.ToList<string>();
                var parser = new ModelParser();
                try
                {
                    IReadOnlyDictionary<Dtmi, DTEntityInfo> om = await parser.ParseAsync(modelList);
                    Log.Out("");
                    Log.Ok($"**********************************************");
                    Log.Ok($"** Validated all files - Your DTDL is valid **");
                    Log.Ok($"**********************************************");
                    Log.Out($"Found a total of {om.Keys.Count()} entities in the DTDL");

                    try
                    {
                        await client.CreateModelsAsync(modelList);
                        Log.Ok($"**********************************************");
                        Log.Ok($"** Models uploaded successfully **************");
                        Log.Ok($"**********************************************");
                    }
                    catch (RequestFailedException ex)
                    {
                        Log.Error($"*** Error uploading models: {ex.Status}/{ex.ErrorCode}");
                        return;
                    }
                }
                catch (ParsingException pe)
                {
                    Log.Error($"*** Error parsing models");
                    int derrcount = 1;
                    foreach (ParsingError err in pe.Errors)
                    {
                        Log.Error($"Error {derrcount}:");
                        Log.Error($"{err.Message}");
                        Log.Error($"Primary ID: {err.PrimaryID}");
                        Log.Error($"Secondary ID: {err.SecondaryID}");
                        Log.Error($"Property: {err.Property}\n");
                        derrcount++;
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Get a twin with the specified id in a cycle
        /// </summary>
        /// <param name='twin_id0'>
        /// Id of an existing twin
        /// </param>
        public Task CommandObserveProperties(string[] cmd)
        {
            if (cmd.Length < 3)
            {
                Log.Error("Please provide at least one pair of twin-id and property name to observe");
                return Task.CompletedTask;
            }
            string[] args = cmd.Skip(1).ToArray();
            if (args.Length % 2 != 0 || args.Length > 8)
            {
                Log.Error("Please provide pairs of twin-id and property names (up to 4) to observe");
                return Task.CompletedTask;
            }
            Log.Alert($"Starting observation...");
            var state = new TimerState
            {
                Arguments = args
            };
            var stateTimer = new Timer(CheckState, state, 0, 2000);
            Log.Alert("Press any key to end observation");
            Console.ReadKey(true);
            state.IsActive = false;
            stateTimer.Dispose();

            return Task.CompletedTask;
        }

        private class TimerState
        {
            public bool IsActive { get; set; } = true;
            public string[] Arguments { get; set; }
        }

        private void CheckState(object state)
        {
            TimerState ts = state as TimerState;
            if (ts == null || ts.IsActive == false)
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

        // Cast a string to its intended type
        public object ConvertStringToType(string schema, string val)
        {
            switch (schema)
            {
                case "boolean":
                    return bool.Parse(val);
                case "double":
                    return double.Parse(val);
                case "float":
                    return float.Parse(val);
                case "integer":
                case "int":
                    return int.Parse(val);
                case "datetime":
                    return DateTime.Parse(val);
                case "duration":
                    return int.Parse(val);
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
                Console.WriteLine(PrettifyJson(res));
        }

        // Log temperature changes in sample app
        public void LogProperty(string res, string propName = "Temperature")
        {
            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(res);

            if (!obj.TryGetValue("$dtId", out object dtid))
                dtid = "<$dtId not found>";

            if (!obj.TryGetValue(propName, out object value))
                value = "<property not found>";

            Console.WriteLine($"$dtId: {dtid}, {propName}: {value}");
        }

        private string PrettifyJson(string json)
        {
            object jsonObj = JsonSerializer.Deserialize<object>(json);
            return JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions { WriteIndented = true });
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///
        /// Console UI
        ///
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private struct CliInfo
        {
            public string Help { get; set; }
            public Func<string[], Task> Command { get; set; }
            public CliCategory Category { get; set; }
        }

        private enum CliCategory
        {
            ADTModels,
            ADTTwins,
            ADTQuery,
            ADTRoutes,
            SampleScenario,
            SampleTools,
        }

        private Dictionary<string, CliInfo> commands;
        private void CliInitialize()
        {
            commands = new Dictionary<string, CliInfo>
            {
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
                { "GetRelationship", new CliInfo { Command=CommandGetRelationshipAsync, Category = CliCategory.ADTTwins, Help="<source-twin-id> <relationship-id>" } },
                { "GetIncomingRelationships", new CliInfo { Command=CommandGetIncomingRelationships, Category = CliCategory.ADTTwins, Help="<source-twin-id>" } },
                { "CreateEventRoute", new CliInfo { Command=CommandCreateEventRoute, Category = CliCategory.ADTRoutes, Help="<route-id> <endpoint-id> <filter>" } },
                { "GetEventRoute", new CliInfo { Command=CommandGetEventRoute, Category = CliCategory.ADTRoutes, Help="<route-id>" } },
                { "GetEventRoutes", new CliInfo { Command=CommandGetEventRoutes, Category = CliCategory.ADTRoutes, Help="" } },
                { "DeleteEventRoute", new CliInfo { Command=CommandDeleteEventRoute, Category = CliCategory.ADTRoutes, Help="<route-id>" } },
                { "SetupBuildingScenario", new CliInfo { Command=CommandSetupBuildingScenario, Category = CliCategory.SampleScenario, Help="loads a set of models and creates a very simple example twins graph" } },
                { "ObserveProperties", new CliInfo { Command=CommandObserveProperties, Category = CliCategory.SampleScenario, Help="<twin id> <propertyName> <twin-id> <property name>... observes the selected properties on the selected twins" } },
                { "DeleteAllTwins", new CliInfo { Command=CommandDeleteAllTwins, Category = CliCategory.SampleTools, Help="Deletes all the twins in your instance" } },
                { "DeleteAllModels", new CliInfo { Command=CommandDeleteAllModels, Category = CliCategory.SampleTools, Help="Deletes all models in your instance" } },
                { "LoadModelsFromDirectory", new CliInfo { Command=CommandLoadModels, Category = CliCategory.SampleTools, Help="<directory-path> <extension(json by default)> [nosub]" } },
                { "Exit", new CliInfo { Command=CommandExit, Category = CliCategory.SampleTools, Help="Exits the program" } },
            };
        }

        public Task CommandExit(string[] args = null)
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }

        public Task CommandHelp(string[] args = null)
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

            return Task.CompletedTask;
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
                    string[] commandArr = SplitArgs(command);
                    string verb = commandArr[0].ToLower();
                    if (!string.IsNullOrEmpty(verb))
                    {
                        var cmd = commands
                            .Where(p => p.Key.ToLower() == verb)
                            .Select(p => p.Value.Command)
                            .Single();
                        await cmd(commandArr);
                    }
                }
                catch (Exception)
                {
                    Log.Error("Invalid command. Please type 'help' for more information.");
                }
            }
        }

        private string[] SplitArgs(string arg)
        {
            int quotecount = arg.Count(x => x == '"');
            if (quotecount % 2 != 0)
            {
                Log.Alert("Your command contains an uneven number of quotes. Was that intended?");
            }
            string[] segments = arg.Split('"', StringSplitOptions.RemoveEmptyEntries);
            var elements = new List<string>();
            for (int i = 0; i < segments.Length; i++)
            {
                if (i % 2 == 0)
                {
                    string[] parts = segments[i].Split(new char[] { }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string ps in parts)
                        elements.Add(ps.Trim());
                }
                else
                {
                    elements.Add(segments[i].Trim());
                }
            }
            return elements.ToArray();
        }
    }
}