using System.Threading.Tasks;

namespace SampleClientApp
{
    public class BuildingScenario
    {
        private readonly CommandLoop cl;
        public BuildingScenario(CommandLoop cl)
        {
            this.cl = cl;
        }

        public async Task InitBuilding()
        {
            Log.Alert($"Deleting all twins...");
            await cl.DeleteAllTwinsAsync();
            Log.Out($"Creating 1 floor, 1 room and 1 thermostat...");
            await InitializeGraph();
        }

        private async Task InitializeGraph()
        {
            string[] modelsToUpload = new string[3] {"CreateModels", "ThermostatModel", "SpaceModel" };
            Log.Out($"Uploading {string.Join(", ", modelsToUpload)} models");

            await cl.CommandCreateModels(modelsToUpload);

            Log.Out($"Creating SpaceModel and Thermostat...");
            await cl.CommandCreateDigitalTwin(new string[15]
                {
                    "CreateTwin", "dtmi:contosocom:DigitalTwins:Space;1", "floor1",
                    "DisplayName", "string", "Floor 1",
                    "Location", "string", "Puget Sound",
                    "Temperature", "double", "0",
                    "ComfortIndex", "double", "0"
                });
            await cl.CommandCreateDigitalTwin(new string[15]
                {
                    "CreateTwin", "dtmi:contosocom:DigitalTwins:Space;1", "room21",
                    "DisplayName", "string", "Room 21",
                    "Location", "string", "Puget Sound",
                    "Temperature", "double", "0",
                    "ComfortIndex", "double", "0"
                });
            await cl.CommandCreateDigitalTwin(new string[18]
                {
                    "CreateTwin", "dtmi:contosocom:DigitalTwins:Thermostat;1", "thermostat67",
                    "DisplayName", "string", "Thermostat 67",
                    "Location", "string", "Puget Sound",
                    "FirmwareVersion", "string", "1.3.9",
                    "Temperature", "double", "0",
                    "ComfortIndex", "double", "0"
                });

            Log.Out($"Creating edges between the Floor, Room and Thermostat");
            await cl.CommandCreateRelationship(new string[11]
                {
                    "CreateEdge", "floor1", "contains", "room21", "floor_to_room_edge",
                    "ownershipUser", "string", "Contoso",
                    "ownershipDepartment", "string", "Comms Division"
                });
            await cl.CommandCreateRelationship(new string[11]
                {
                    "CreateEdge", "room21", "contains", "thermostat67", "room_to_therm_edge",
                    "ownershipUser", "string", "Contoso",
                    "ownershipDepartment", "string", "Comms Division"
                });
        }
    }
}
