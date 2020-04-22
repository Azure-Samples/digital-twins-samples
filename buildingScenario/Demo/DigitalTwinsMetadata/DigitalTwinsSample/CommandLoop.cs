using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using ADTApi;
using ADTApi.Models;
using Newtonsoft.Json;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace DigitalTwinsSample
{
    public class CommandLoop
    {
        private AzureDigitalTwinsAPI client;
        private IScenario currScen;
        public CommandLoop(AzureDigitalTwinsAPI _client)
        {
            client = _client;
        }

        public async Task run()
        {
            while (true)
            {
                try
                {
                    Log("\nEnter a command or 'help'.");
                    string command = Console.ReadLine().Trim();
                    string[] commandArr = command.Split(null);
                    if (commandArr[0] == "help") CommandHelp();
                    else if (commandArr[0] == "addModels" & commandArr.Length >= 2) SubmitAddModels(commandArr.Skip(1).ToArray()).Wait();
                    else if (commandArr[0] == "decommissionModel" & commandArr.Length == 3) SubmitDecommissionModel(commandArr[1], commandArr[2]).Wait();
                    else if (commandArr[0] == "getModelById" & commandArr.Length == 2) SubmitGetModelById(commandArr[1]).Wait();
                    else if (commandArr[0] == "getModelById" & commandArr.Length == 3) SubmitGetModelById(commandArr[1], commandArr[2]).Wait();
                    else if (commandArr[0] == "listModels" & commandArr.Length == 1) SubmitListModels().Wait();
                    else if (commandArr[0] == "listModels" & commandArr.Length == 2) SubmitListModels(commandArr[1]).Wait();
                    else if (commandArr[0] == "listModels" & commandArr.Length >= 3) SubmitListModels(commandArr[1], commandArr.Skip(2).ToArray()).Wait();
                    else if (commandArr[0] == "queryTwins" & command.Length == 10) SubmitQueryTwins("").Wait();
                    else if (commandArr[0] == "queryTwins" & commandArr.Length > 1) SubmitQueryTwins(command.Substring(11)).Wait();
                    else if (commandArr[0] == "addTwin" & commandArr.Length % 3 == 0) SubmitAddTwin(commandArr[1], commandArr.Skip(3).ToArray(), commandArr[2]).Wait();
                    else if (commandArr[0] == "updateTwin" & (commandArr.Length - 2) % 4 == 0) SubmitUpdateTwin(commandArr[1], commandArr.Skip(2).ToArray()).Wait();
                    else if (commandArr[0] == "getTwinById" & commandArr.Length == 2) SubmitGetTwinById(commandArr[1]).Wait();
                    else if (commandArr[0] == "cycleGetTwinById" & commandArr.Length == 2) SubmitCycleGetTwinById(commandArr[1], 20).Wait();
                    else if (commandArr[0] == "cycleGetTwinById" & commandArr.Length == 3) SubmitCycleGetTwinById(commandArr[1], 20, commandArr[2]).Wait();
                    else if (commandArr[0] == "deleteTwin" & commandArr.Length == 2) SubmitDeleteTwin(commandArr[1]).Wait();
                    else if (commandArr[0] == "addEdge" & (commandArr.Length - 5) % 3 == 0) SubmitAddEdge(commandArr[1], commandArr[2], commandArr[3], commandArr.Skip(5).ToArray(), commandArr[4]).Wait();
                    else if (commandArr[0] == "deleteEdge" & commandArr.Length == 4) SubmitDeleteEdge(commandArr[1], commandArr[2], commandArr[3]).Wait();
                    else if (commandArr[0] == "listEdges" & commandArr.Length == 2) SubmitListEdges(commandArr[1]).Wait();
                    else if (commandArr[0] == "getEdgeById" & commandArr.Length == 4) SubmitGetEdgeById(commandArr[1], commandArr[2], commandArr[3]).Wait();
                    else if (commandArr[0] == "buildingScenario" & commandArr.Length == 1) BuildingScenario().Wait();
                    else if (commandArr[0] == "exit") break;
                    else Log("Invalid Command");
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                    break;
                }
            }
        }

        /// <summary>
        /// Uploads a model from a DTDL interface (often a JSON file)
        /// </summary>
        /// <param name='modelArray'>
        /// Array of names of interface/interface file to upload. Must be in /ConsoleApp1/ConsoleApp1/Models folder
        /// </param>
        public async Task SubmitAddModels(string[] modelArray)
        {
            string filename;
            string[] filenameArray = new string[modelArray.Length];
            for (int i = 0; i < filenameArray.Length; i++)
            {
                filenameArray[i] = !(modelArray[i].EndsWith(".json") | modelArray[i].EndsWith(".dtdl")) ? $"{modelArray[i]}.json": modelArray[i];
            }
            Console.WriteLine(string.Format("Submitting models: {0}...", string.Join(", ", filenameArray)));
            try
            {
                string consoleAppDir = Directory.GetParent(Directory.GetParent((Directory.GetParent(Environment.CurrentDirectory).ToString())).ToString()).ToString();
                List<object> dtdlList = new List<object>();
                for (int i = 0; i < filenameArray.Length; i++)
                {
                    filename = consoleAppDir + @"\Models\" + filenameArray[i];
                    StreamReader r = new StreamReader(filename);
                    string dtdl = r.ReadToEnd();
                    r.Close();
                    dtdlList.Add(JsonConvert.DeserializeObject(dtdl));
                }
                var res = await client.Models.AddAsync(dtdlList);
                Log($"Model(s) created successfully!");
                LogResponse(res);
            }
            catch (ErrorResponseException ex)
            {
                Log($"Error: {ex.Response.Content}");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Decommission a model (disable it from being created)
        /// </summary>
        /// <param name='model_id'>
        /// Id of the model to be decommissioned
        /// A model id looks like "urn:example:Floor:1"
        /// </param>
        public async Task SubmitDecommissionModel(string model_id, string val)
        {
            Log($"Submitting...");
            try
            {
                List<object> body = new List<object>();
                body.Add(new Dictionary<string, object>(){
                        { "value", bool.Parse(val) },
                        { "path", "/decommissioned" },
                        { "op", "replace" }
                    });
                await client.Models.UpdateAsync(model_id, body);
                Log($"Model decommissioned successfully!");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
        }

        /// <summary>
        /// Gets all models
        /// </summary>
        public async Task SubmitListModels(string include_model_definition = "false", string[] dependencies_for = null)
        {
            int page = 1;
            Log($"Submitting...");
            try
            {
                List<string> dependencies_for_list = new List<string>();
                if (dependencies_for != null) dependencies_for_list.AddRange(dependencies_for);
                PagedModelDataCollection res = await client.Models.ListAsync(dependencies_for, bool.Parse(include_model_definition), new ModelsListOptions());
                Log($"***Get Models results page {page}; {res/*.Value.Count*/} results");
                LogResponse(res);
                Log($"Continuation Token: {extractContinuationToken(res)}");
                page++;
                /*while (res.NextLink != null && res.Value != null)
                {
                    string contToken = extractContinuationToken(res);
                    Log($"***Get Models results page {page}; {res.Value.Count} results");
                    res = await client.Models.ListAsync(null, false, new ModelsListOptions(), contToken);
                    LogResponse(res);
                    Log($"Continuation Token: {contToken}");
                    page++;
                }*/
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets model based on specified Id
        /// </summary>
        /// <param name='model_id'>
        /// Name of a model you are trying to make a Twin out of. The model must have already been uploaded to your instance.
        /// A model_id looks like "urn:example:ConferenceRoom:1"
        /// </param>
        /// <param name='include_model_definition'>
        /// Variable to indicate whether full model defintion information should be included in response data
        /// "true" or "false"
        /// </param>
        public async Task SubmitGetModelById(string model_id, string include_model_definition = "false")
        {
            Log($"Submitting...");
            try
            {
                var res = await client.Models.GetByIdAsync(model_id, bool.Parse(include_model_definition));
                LogResponse(res);
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Query your digital twins graph
        /// </summary>
        /// <param name='query'>
        /// SQL-like query that you're requesting
        /// </param>
        public async Task<List<QueryResult>> SubmitQueryTwins(string query)
        {
            int page = 1;
            List<QueryResult> results = new List<QueryResult>();
            if (query.Trim().Length == 0) query = "SELECT * FROM DIGITALTWINS";
            QuerySpecification req = new QuerySpecification(query);
            Log($"Submitting query: {query}...");
            try
            {
                QueryResult qr = await client.Query.QueryTwinsAsync(req);
                Log($"***Query results page {page}; {qr.Items.Count} results");
                results.Add(qr);
                LogResponse(qr);
                Log($"Continuation Token: {qr.ContinuationToken}");
                page++;
                while (qr.ContinuationToken != null && qr.ContinuationToken != "" && qr.Items != null)
                {
                    Log($"***Query results page {page}; {qr.Items.Count} results");
                    req.ContinuationToken = qr.ContinuationToken;
                    qr = await client.Query.QueryTwinsAsync(req);
                    results.Add(qr);
                    LogResponse(qr);
                    Log($"Continuation Token: {qr.ContinuationToken}");
                    page++;
                }
                return results;
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Create a twin with the specified properties
        /// </summary>
        /// <param name='model_id'>
        /// Id of the model the instantiated twin should reference
        /// A model_id looks like "urn:example:ConferenceRoom:1"
        /// </param>
        /// <param name='twin_id'>
        /// User defined unique twin id for the instantiated twin
        /// </param>
        /// <param name='args'>
        /// An array where {args[n+1], args[n+2] and args[n+3]} are {property name, property schema and property value} for the nth property
        /// A property schema is the primitive type (i.e. string, integer, bool, etc.) for the value
        /// </param>
        public async Task SubmitAddTwin(string model_id, string[] args, string twin_id = null)
        {
            if (twin_id == null) twin_id = Guid.NewGuid().ToString();
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
            LogResponse(twinData);
            Log($"Submitting...");

            object res = null;
            try
            {
                res = await client.DigitalTwins.AddAsync(twin_id, twinData);
                Log($"Twin '{twin_id}' created successfully!");
            }
            catch (ErrorResponseException ex)
            {
                Log($"Error: {ex.Response.Content}");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
        }

        /// <summary>
        /// Delete a twin with the specified id 
        /// </summary>
        /// <param name='twin_id'>
        /// Id of a twin to delete
        /// </param>
        public async Task SubmitDeleteTwin(string twin_id)
        {
            Log($"Submitting...");
            try
            {
                await client.DigitalTwins.DeleteAsync(twin_id);
                Log($"Twin '{twin_id}' deleted successfully!");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a twin with the specified id 
        /// </summary>
        /// <param name='twin_id'>
        /// Id of an existing twin
        /// </param>
        public async Task SubmitGetTwinById(string twin_id)
        {
            Log($"Submitting...");
            try
            {
                var res = await client.DigitalTwins.GetByIdAsync(twin_id);
                LogResponse(res);
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
        }

        /// <summary>
        /// Get a twin with the specified id in a cycle
        /// </summary>
        /// <param name='twin_id0'>
        /// Id of an existing twin
        /// </param>
        public async Task SubmitCycleGetTwinById(string twin_id0, int cycleNumber, string twin_id1 = null)
        {
            Log($"Submitting...");
            try
            {
                while (cycleNumber >= 0)
                {
                    var res = await client.DigitalTwins.GetByIdAsync(twin_id0);
                    LogTemp(res);
                    if (twin_id1 != null)
                    {
                        var res2 = await client.DigitalTwins.GetByIdAsync(twin_id1);
                        LogTemp(res2);
                    }
                    cycleNumber--;
                    Thread.Sleep(5000);
                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
        }

        /// <summary>
        /// Update specific properties of a twin
        /// </summary>
        /// <param name='twin_id'>
        /// Twin id for the t-be-updated twin
        /// </param>
        /// <param name='args'>
        /// An array where {args[n], args[n+1], args[n+2] and args[n+3]} are {update operation, property path, property schema and property value} for the nth property
        /// A property path looks like "values/Temperature"
        /// A property schema is the primitive type (i.e. string, integer, bool, etc.) for the value
        /// </param>
        public async Task SubmitUpdateTwin(string twin_id, string[] args)
        {
            List<object> twinData = new List<object>();
            for (int i = 0; i < args.Length; i += 4)
            {
                twinData.Add(new Dictionary<string, object>() {
                    { "op", args[i]},
                    { "path", args[i + 1]},
                    { "value", convertStringToType(args[i + 2], args[i + 3])}
                });
            }
            Log($"Submitting...");
            try
            {
                var res = await client.DigitalTwins.UpdateAsync(twin_id, twinData);
                if (res != null) LogResponse(((ErrorResponse)res).Error);
                else Log($"Twin '{twin_id}' updated successfully!");

            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
        }

        /// <summary>
        /// Create an edge between a source twin and target twin with the specified relationship name
        /// </summary>
        /// <param name='source_twin'>
        /// Id of the twin where the edge is pointing FROM
        /// </param>
        /// <param name='relationship_name'>
        /// Name of the relationship. It must exist as a "relationship" in the source twin dtdl model
        /// </param>
        /// <param name='target_twin'>
        /// Id of the twin where the edge is pointing TO
        /// </param>
        /// <param name='args'>
        /// An array where {args[n+1], args[n+2] and args[n+3]} are {property name, property schema and property value} for the nth property
        /// A property schema is the primitive type (i.e. string, integer, bool, etc.) for the value
        /// </param>
        /// <param name='edge_id'>
        /// Optional ID of the edge being created

        /// </param>
        public async Task SubmitAddEdge(string source_twin_id, string relationship_name, string target_twin_id, string[] args, string edge_id = null)
        {
            if (edge_id == null) edge_id = Guid.NewGuid().ToString();
            Dictionary<string, object> body = new Dictionary<string, object>()
            {
                { "$targetId", target_twin_id},
            };
            for (int i = 0; i < args.Length; i += 3)
            {
                body.Add(args[i], convertStringToType(args[i + 1], args[i + 2]));
            }
            Log($"Submitting...");
            try
            {
                object res = await client.DigitalTwins.AddEdgeAsync(source_twin_id, relationship_name, edge_id, body);
                if (res == null) Log($"Edge created successfully!");
                else LogResponse(res);

            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
        }

        /// <summary>
        /// Delete an edge from a source twin with the specified relationship name and edge id
        /// </summary>
        /// <param name='source_twin_id'>
        /// Id of the twin where the edge is pointing FROM
        /// </param>
        /// <param name='relationship_name'>
        /// Name of the relationship. It must exist as a "relationship" in the source twin dtdl model
        /// </param>
        /// <param name='edge_id'>
        /// Id of the edge to be deleted
        /// </param>
        public async Task SubmitDeleteEdge(string source_twin_id, string relationship_name, string edge_id)
        {
            Log($"Submitting...");
            try
            {
                await client.DigitalTwins.DeleteEdgeAsync(source_twin_id, relationship_name, edge_id);
                Log($"Edge '{edge_id}' for twin '{source_twin_id}' of type '{relationship_name}' deleted successfully!");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
        }

        /// <summary>
        /// Get an edge with the specified source twin
        /// </summary>
        /// <param name='source_twin_id'>
        /// Id of the twin where the edge is pointing FROM
        /// </param>
        public async Task<EdgeCollection> SubmitListEdges(string source_twin_id)
        {
            Log($"Submitting...");
            try
            {
                EdgeCollection res = (EdgeCollection) await client.DigitalTwins.ListEdgesAsync(source_twin_id);
                LogResponse(res);
                return res;
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Get an edge with a specified source twin, relationship name and edge id
        /// </summary>
        /// <param name='source_twin_id'>
        /// Id of the twin where the edge is pointing FROM
        /// </param>
        /// <param name='relationship_name'>
        /// Name of the relationship. It must exist as a "relationship" in the source twin dtdl model
        /// </param>
        /// <param name='edge_id'>
        /// Id of the edge
        /// </param>
        public async Task SubmitGetEdgeById(string source_twin_id, string relationship_name, string edge_id)
        {
            Log($"Submitting...");
            try
            {
                var res = await client.DigitalTwins.GetEdgeByIdAsync(source_twin_id, relationship_name, edge_id);
                LogResponse(res);
            }
            catch (Exception ex)
            {
                Log($"Error: {ex}");
            }
        }

        /// <summary>
        /// Get an edge with a specified source twin, relationship name and edge id
        /// </summary>
        /// <param name='source_twin_id'>
        /// Id of the twin where the edge is pointing FROM
        /// </param>
        /// <param name='relationship_name'>
        /// Name of the relationship. It must exist as a "relationship" in the source twin dtdl model
        /// </param>
        /// <param name='edge_id'>
        /// Id of the edge
        /// </param>
        public async Task BuildingScenario()
        {
            Log($"Initializing Building Scenario...");
            currScen = new BuildingScenario(this);
            await currScen.init();
        }

        //THIS IS NOT TESTED OR COMPLETE
        public async Task SubmitUpdateEdge(string id)
        {

        }

        //THIS IS NOT TESTED OR COMPLETE
        public async Task SubmitGetRelationship(string id)
        {

        }

        //THIS IS NOT TESTED OR COMPLETE
        public async Task SubmitGetRelationshipById(string id)
        {

        }

        //THIS IS NOT TESTED OR COMPLETE
        public async Task SubmitGetRelationshipByRelationshipName(string id)
        {

        }

        //THIS IS NOT TESTED OR COMPLETE
        public async Task SubmitGetComponent(string id)
        {

        }

        //THIS IS NOT TESTED OR COMPLETE
        public async Task SubmitUpdateComponent(string id)
        {

        }

        //THIS IS NOT TESTED OR COMPLETE
        public async Task SubmitTelemetry(string id)
        {

        }

        // Display available commands
        private void CommandHelp()
        {
            Log("\n addModels <model-filename-0> <model-filename-1> ..." +
                "\n decommissionModel <model-id> <true/false>" +
                "\n listModels" +
                "\n getModelById <model-id>" +
                "\n queryTwins <query-string>" +
                "\n addTwin <model-id> <twin-id> <property-name-0> <prop-type-0> <prop-value-0> ..." +
                "\n deleteTwin <twin-id>" +
                "\n getTwinById <twin-id>" +
                "\n cycleGetTwinById <twin-id>" +
                "\n cycleGetTwinById <twin-id-0> <twin-id-1>" +
                "\n updateTwin <twin-id> <operation-0> <path-0> <value-schema-0> <value-0> ..." +
                "\n addEdge <source-twin-id> <relationship-name> <target-twin-id> <edge-id> <property-name-0> <prop-type-0> <prop-value-0> ..." +
                "\n deleteEdge <source-twin-id> <relationship-name> <edge-id>" +
                "\n listEdges <twin-id>" +
                "\n getEdgeById <source-twin-id> <relationship-name> <edge-id>" +
                "\n buildingScenario" +
                "\n exit\n");
        }

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

        // Return continuation token from a response
        public string extractContinuationToken(PagedModelDataCollection res)
        {
            if (res.NextLink == null) return "null";
            LogResponse(res);
            var queryParameters = new Uri(new Uri("http://baseuri./com"), res.NextLink).Query;
            var parsedQueryParameters = HttpUtility.ParseQueryString(queryParameters);
            string contToken = parsedQueryParameters.GetValues("continuationToken").First();
            return contToken;
        }

        // Log a string to the command prompt
        public void Log(string s, string type = "")
        {
            if (type == "green")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(s);
                Console.ForegroundColor = ConsoleColor.White;
            }
            else Console.WriteLine(s);
        }

        // Log a JSON serialized object to the command prompt
        public void LogResponse(object res, string type = "")
        {
            if (type != "") Console.WriteLine($"{type}: \n");
            else Console.WriteLine("Response:");
            string res_json = JsonConvert.SerializeObject(res, Formatting.Indented);
            Console.WriteLine(res_json);
        }

        //Log temperature changes in sample app
        public void LogTemp(object res)
        {
            Dictionary<string, object> o = JObject.FromObject(res).ToObject<Dictionary<string, object>>();
            Console.WriteLine($"$dtId: {o["$dtId"]}, Temperature: {o["Temperature"]}");
        }
    }
}
