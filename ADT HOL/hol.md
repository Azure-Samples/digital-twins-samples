# Setup ADT
## Setup variables
First, we'll need to create and store some variables in the Azure Cloud Shell. This will make running the commands needed in the subsequent units easier and avoid mistakes from typos.

1. Make sure that you've activated the sandbox using the button above. The sandbox will allow us to create the necessary resources without incurring any charges.
1. If you're running this outside of the MSLearn environment, make sure the CLI is set to **Bash**
1. Setup the variables that will be used by the commands in the rest of this module. Copy and paste the following into the CLI.

> [!TIP]
> dtname & functionname must be globally unique and **LOWERCASE**
>
> The sandbox environment provides a single resource group. If you're running this exercise outside of the lab environment, rgname will have to be set manually.  
>
> For non-sandbox environments, create a Resrouce Group using the following command: 
*az group create -n "my rg name"*
>
>**Save these values for use later**

```azurecli
rgname=adthol-$RANDOM
dtname=$rgname
location=eastus
username=<msa account used to log into sandbox>
functionname=$rgname
```
## Use the CLI to deploy ADT

1. Create Azure Digital Twins
    ```azurecli
   az dt create --dt-name $dtname -g $rgname -l $location
    ```
1. Assign permissions to Azure Digital Twins

    ```azurecli
    az dt role-assignment create -n $dtname -g $rgname --role "Azure Digital Twins Data Owner" --assignee $username -o json
    ```
1. Create manifest.json for use later

    ```azurecli
    touch manifest.json
    cat > manifest.json
    ```
1. Now you're editing manifest.json
1. Paste the JSON code below into the shell and use ctrl+C to close the file

    ```json
    [{
        "resourceAppId": "0b07f429-9f4b-4714-9392-cc5e8e80c8b0",
        "resourceAccess": [
         {
           "id": "4589bd03-58cb-4e6c-b17f-b580e39652f8",
           "type": "Scope"
         }
        ]
    }]
    
    ```

1. Create an Azure AD application with permissions to connect to Digital Twins.  This will be used in later units.
    ```azurecli
    az ad app create --display-name $functionname --native-app --required-resource-accesses ./manifest.json --reply-url http://localhost -o json 
    ```
1. The command below will output the Application ID. Save this for use later.
    ```azurecli
    az ad app list --display-name $functionname --query '[0].appId' -o json
    ```
1. Create a Service Principal for the App ID. Add the App ID from above to the command below before running.
    ```azurecli
    az ad sp create --id 3e16965a-64f5-4639-91d2-ef460885de7d
    ```
1. The command below assign permissions to the application created above to the ADT instance. Add the App ID from above to the command below before running.
    ```azurecli
    az dt role-assignment create --dt-name $dtname --assignee "<app ID from above>" --role "Azure Digital Twins Data Owner"
    ```

1. Create a password for the application.
  > [!NOTE]
> Make sure you copy the password from the output. This can't be retrieved later.  If you lose your secret/password you'll have to create a new one
>
```azurecli
az ad app credential reset --id <app id above> --append
```

## Collect important values

There are several important values from the resources set that you will need as you continue working with your Azure Digital Twins instance.
>[!NOTE] Save These values for use later
>
### Collect instance values
1. Get the hostname of the Digital Twins instance. Copy the output to notepad for use later.
    ```azurecli
    az dt show -n $dtname --query 'hostName'
    ```
### Collect app registration values
1. Get the Azure Active Directory (AAD) Tennant ID
    ```azurecli
    az account show --query 'tenantId'
    ```

## Setup ADT Models
You can add/upload a model using the CLI command below, and then create a twin using this model that will be updated with information from IoT Hub.

The model looks like this:
```JSON
{
  "@id": "dtmi:contosocom:DigitalTwins:Thermostat;1",
  "@type": "Interface",
  "@context": "dtmi:dtdl:context;2",
  "contents": [
    {
      "@type": "Property",
      "name": "Temperature",
      "schema": "double"
    }
  ]
}
```
1. Upload this model to your twins instance by running the following command in the Azure shell from the previous unit

    ```azurecli
    az dt model create --models '{  "@id": "dtmi:contosocom:DigitalTwins:Thermostat;1",  "@type": "Interface",  "@context": "dtmi:dtdl:context;2",  "contents": [    {      "@type": "Property",      "name": "Temperature",      "schema": "double"    }, {      "@type": "Property",      "name": "RESTAPI",      "schema": "double"    }, {      "@type": "Property",      "name": "LOGICAPP",      "schema": "double"    }  ]}' -n $dtname
    ```
1. Use the following command to create a twin and set 0.0 as an initial temperature value.

    ```azurecli
    az dt twin create --dtmi "dtmi:contosocom:DigitalTwins:Thermostat;1" --twin-id thermostat67 --properties '{"Temperature": 0.0, "RESTAPI": 0.0, "LOGICAPP": 0.0}' --dt-name $dtname
    ```
Output of a successful twin create command should look like this:
```json
{
  "$dtId": "thermostat67",
  "$etag": "W/\"911fc8fa-8ffb-4c22-b7f3-ed939f4f8c64\"",
  "$metadata": {
    "$model": "dtmi:contosocom:DigitalTwins:Thermostat;1",
    "LOGICAPP": {
      "lastUpdateTime": "2020-10-26T19:27:20.1460603Z"
    },
    "RESTAPI": {
      "lastUpdateTime": "2020-10-26T19:27:20.1460603Z"
    },
    "Temperature": {
      "lastUpdateTime": "2020-10-26T19:27:20.1460603Z"
    }
  },
  "LOGICAPP": 0.0,
  "RESTAPI": 0.0,
  "Temperature": 0.0
}
```

# Setup Function to Ingest Events from IoT Hub
We can ingest data into Azure Digital Twins through external compute resources, such as an Azure function, that receives the data and uses the DigitalTwins APIs to set properties.

## Configure your environment
- [Visual Studio Code](https://code.visualstudio.com/) on one of the [supported platforms](https://code.visualstudio.com/docs/supporting/requirements#_platforms).
- The [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) for Visual Studio Code.
- The [Azure Functions extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions) for Visual Studio Code.

## Create an Azure Function via CLI
1. Create a Azure storage account
    ```azurecli
    az storage account create --name $functionname --location $location --resource-group $rgname --sku Standard_LRS
    ```
1. Create an Azure Function
    ```azurecli
    az functionapp create --resource-group $rgname --consumption-plan-location $location --runtime dotnet --functions-version 3 --name $functionname --storage-account $functionname
    ```
## Configure security access for the Azure function app
The Azure function skeleton from earlier examples requires that a bearer token to be passed in order to authenticate with Azure Digital Twins. To make sure that this bearer token is passed, you'll need to create a [Managed Service Identity (MSI)](../active-directory/managed-identities-azure-resources/overview.md) for the function app.

In this section, we'll create a system-managed identity and assign the function app's identity to the _Azure Digital Twins Owner (Preview)_ role for your Azure Digital Twins instance. The Managed Identity gives the function app permission in the instance to perform data plane activities. We'll also provide the the URL of Azure Digital Twins instance to the function by setting an environment variable.

1. Use the following command to create the system-managed identity. Take note of the _principalId_ field in the output.

    ```azurecli	
    az functionapp identity assign -g $rgname -n $functionname	
    ```
1. Use the _principalId_ value in the following command to assign the function app's identity to the _Azure Digital Twins Data Owner_ role for your Azure Digital Twins instance.

    ```azurecli	
    az dt role-assignment create --dt-name $dtname --assignee "<principal-ID>" --role "Azure Digital Twins Data Owner"
    ```
Lastly, set the URL of your Azure Digital Twins as an environment variable

> [!TIP]
> The Azure Digital Twins instance's URL is made by adding *https://* to the beginning of your Azure Digital Twins instance's *hostName* which you retrieved earlier.

```azurecli
   az functionapp config appsettings set -g $rgname -n $functionname --settings "ADT_SERVICE_URL=https://<your-Azure-Digital-Twins-instance-hostname>"
```


## Create an Azure Functions app in Visual Studio Code
In this section, you use Visual Studio Code to create a local Azure Functions project in your chosen language. Later in this article, you'll publish your function code to Azure.

1. Choose the Azure icon in the Activity bar, then in the **Azure: Functions** area, select the **Create new project...** icon.

    ![Choose Create a new project](../media/create-new-project.png)

1. Choose a directory location for your project workspace and choose **Select**.


1. Provide the following information at the prompts:
    - **Select a language for your function project**: Choose `C#`.
    - **Select a template for your project's first function**: Choose `Change template filter`.
    - **Select a template filter**: Choose All
    - **Select a template for your project's first function**: Choose `EventGridTrigger`.
    - **Provide a function name**: Type `TwinsFunction`.
    - **Provide a namespace**: Type `My.Function`.
    - **When prompted for a storage account choose**: Skip for now
    - **Select how you would like to open your project**: Choose `Add to workspace`.

### Install Nuget packages
In the Visual Studio Code Terminal, add the required Nuget packages by typing the following commands:

```dos
    dotnet add package Azure.DigitalTwins.Core --version 1.0.0-preview.3
    dotnet add package Azure.identity --version 1.2.2
    dotnet add package System.Net.Http
```

### Write an Azure function with an Event Grid trigger
1. In VS Code, open the file TwinsFunction.cs
1. Replace the code in the Function App template with the sample provided:
>[!TIP]
>The namespace and function name must match.  If you changed them in the previous steps, make sure to do the same in the code sample.

```csharp
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.DigitalTwins.Core;
using Azure.DigitalTwins.Core.Serialization;
using Azure.Identity;
using System.Net.Http;
using Azure.Core.Pipeline;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace My.Function
{
    public class TwinsFunction
    {
        //Your Digital Twin URL is stored in an application setting in Azure Functions
        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("TwinsFunction")]
        public async void Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {
            log.LogInformation(eventGridEvent.Data.ToString());
            if (adtInstanceUrl == null) log.LogError("Application setting \"ADT_SERVICE_URL\" not set");
            try
            {
                //Authenticate with Digital Twins
                ManagedIdentityCredential cred = new ManagedIdentityCredential("https://digitaltwins.azure.net");
                DigitalTwinsClient client = new DigitalTwinsClient(new Uri(adtInstanceUrl), cred, new DigitalTwinsClientOptions { Transport = new HttpClientTransport(httpClient) });
                log.LogInformation($"ADT service client connection created.");
                if (eventGridEvent != null && eventGridEvent.Data != null)
                {
                    log.LogInformation(eventGridEvent.Data.ToString());

                    // Reading deviceId and temperature for IoT Hub JSON
                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
                    string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];
                    var temperature = deviceMessage["body"]["Temperature"];
                    
                    log.LogInformation($"Device:{deviceId} Temperature is:{temperature}");

                    //Update twin using device temperature
                    var uou = new UpdateOperationsUtility();
                    uou.AppendReplaceOp("/Temperature", temperature.Value<double>());
                    await client.UpdateDigitalTwinAsync(deviceId, uou.Serialize());
                }
            }
            catch (Exception e)
            {
                log.LogError(e.Message);
            }

        }
    }
}
```

## Publish the function app to Azure
1. In the VSCode function extension, click on on **Deploy to Function App...**
    ![Choose Deploy to Function App...](../media/deploy-to-function-app.png)
- **Select subscription**: Choose `Concierge Subscription` if you're using the sandbox environment
- **Select Function App in Azure**: Choose `<name>twinfunction`.

1. When the deployment finishes, you'll be prompted to Start Streaming Logs
  ![STream Logs](../media/function-stream-logs.png)
1. Click on **Stream Logs** to see the messages received by the Azure Function after the IoT Hub setup in the next step. There won't be any messages received until the IoT Hub is setup and a device sends messages.
1. Alternatively, you can Stream Logs at a later time by right-clicking on the Azure Function in VS Code and choosing **Start Streaming Logs**
  ![Choose Deploy to Function App...](../media/function-stream-logs-extension.png)
  
# Setup IoT Hub

1. Run the following [command to create an IoT hub](https://docs.microsoft.com/cli/azure/iot/hub#az-iot-hub-create) in your resource group, using a globally unique name for your IoT hub:

    
   ```azurecli-interactive
   az iot hub create --name $dtname --resource-group $rgname --sku S1
   ```
1. In Azure Cloud Shell, create a device in IoT Hub with the following command:

    ```azurecli
    az iot hub device-identity create --device-id thermostat67 --hub-name $dtname -g $rgname
    ```

The output is information about the device that was created.

## Configure EventGrid for IoT Hub
In this section, you configure your IoT Hub to publish events as they occur. 
```Azure CLI
iothub=$(az iot hub list -g $rgname --query [].id -o tsv | sed -e 's/\r//g')
function=$(az functionapp function show -n $functioname -g $rgname --function-name twinsfunction --query id -o tsv | sed -e 's/\r//g')
az eventgrid event-subscription create --name IoTHubEvents \
                                        --source-resource-id $iothub \
                                       --endpoint $function \
                                       --endpoint-type azurefunction \
                                       --included-event-types Microsoft.Devices.DeviceTelemetry
```

At this point, you should see messages showing up in the Azure Function Log Stream that was configured in the previous unit.  The Azure Function Log Stream will show the telemetry being received from Event Grid and any errors connecting to Azure Digital Twins or updating the Twin.

   ![Log Stream](../media/LogStream.png)

## Send data from a simulated device
```Azure CLI
az iot device simulate -d thermostat67 -n $dtname --data '{ "Temperature": 67.3 }' --msg-count 1
```

## Validate Twin is being updated
1. You can see the values in being updated in the Twin Thermostat67 by running the following command
```azurecli
 az dt twin show -n $dtname --twin-id thermostat67
```

# Create an ADT Route and Filter

## Create Event Hubs
az eventhubs namespace create --name $dtname --resource-group $rgname -l $location

az eventhubs eventhub create --name "twins-event-hub" --resource-group $rgname --namespace-name $dtname

az eventhubs eventhub create --name "tsi-event-hub" --resource-group $rgname --namespace-name $dtname

az eventhubs eventhub authorization-rule create --rights Listen Send --resource-group $rgname --namespace-name $dtname --eventhub-name "twins-event-hub" --name EHPolicy

az eventhubs eventhub authorization-rule create --rights Listen Send --resource-group $rgname --namespace-name $dtname --eventhub-name "tsi-event-hub" --name EHPolicy

## Create  ADT Route
az dt endpoint create eventhub --endpoint-name EHEndpoint --eventhub-resource-group $rgname --eventhub-namespace $dtname --eventhub "twins-event-hub" --eventhub-policy EHPolicy -n $dtname

az dt route create -n $dtname --endpoint-name EHEndpoint --route-name EHRoute --filter "type = 'Microsoft.DigitalTwins.Twin.Update'"

# Create Azure Function
1. Create an Azure Function
    
```azurecli
    az functionapp create --resource-group $rgname --consumption-plan-location $location --runtime dotnet --functions-version 3 --name $ehfunctionname --storage-account  $storagename
  ```

1. Add application config 
adtehconnectionstring=$(az eventhubs eventhub authorization-rule keys list --resource-group $rgname --namespace-name $dtname --eventhub-name twins-event-hub --name EHPolicy --query primaryConnectionString -o tsv)

tsiehconnectionstring=$(az eventhubs eventhub authorization-rule keys list --resource-group $rgname --namespace-name $dtname --eventhub-name tsi-event-hub --name EHPolicy --query primaryConnectionString -o tsv)

az functionapp config appsettings set --settings "EventHubAppSetting-Twins=$adtehconnectionstring" -g $rgname -n $ehfunctionname
az functionapp config appsettings set --settings "EventHubAppSetting-TSI=$tsiehconnectionstring" -g $rgname -n $ehfunctionname


## Create an Azure Functions app in Visual Studio Code
In this section, you use Visual Studio Code to create a local Azure Functions project in your chosen language. Later in this article, you'll publish your function code to Azure.

1. Choose the Azure icon in the Activity bar, then in the **Azure: Functions** area, select the **Create new project...** icon.

    ![Choose Create a new project](../media/create-new-project.png)

1. Choose a directory location for your project workspace and choose **Select**.


1. Provide the following information at the prompts:
    - **Select a language for your function project**: Choose `C#`.
    - **Select a template for your project's first function**: Choose `EventHubTrigger`.
    - **Provide a function name**: Type `TwinsFunction`.
    - **Provide a namespace**: Type `SampleFunctionApp`.
    - **Select setting from local.settings.json**: Hit Enter
    - **Select subscription**: Select the subscription you're using
    - **Select an event hub namespace**: Choose the namespace created above
    - **Select an event hub**: Choose the event hub created above
    - **Select an event hub policy**: Choose `EHPolicy'
    - **When prompted for a storage account choose**: Skip for now
    - **Select how you would like to open your project**: Choose `Add to workspace`.
1. Type the following code 

```C#
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

namespace SampleFunctionsApp
{
    public static class ProcessDTUpdatetoTSI
    { 
        [FunctionName("ProcessDTUpdatetoTSI")]
        public static async Task Run(
            [EventHubTrigger("twins-event-hub", Connection = "EventHubAppSet-ting-Twins")]EventData myEventHubMessage, 
            [EventHub("tsi-event-hub", Connection = "EventHubAppSetting-TSI")]IAsyncCollector<string> outputEvents, 
            ILogger log)
        {
            JObject message = (JOb-ject)JsonConvert.DeserializeObject(Encoding.UTF8.GetString(myEventHubMessage.Body));
            log.LogInformation("Reading event:" + message.ToString());

            // Read values that are replaced or added
            Dictionary<string, object> tsiUpdate = new Dictionary<string, ob-ject>();
            foreach (var operation in message["patch"]) {
                if (operation["op"].ToString() == "replace" || opera-tion["op"].ToString() == "add")
                {
                    //Convert from JSON patch path to a flattened property for TSI
                    //Example input: /Front/Temperature
                    //        output: Front.Temperature
                    string path = operation["path"].ToString().Substring(1);                    
                    path = path.Replace("/", ".");                    
                    tsiUpdate.Add(path, operation["value"]);
                }
            }
            //Send an update if updates exist
            if (tsiUpdate.Count>0){
                tsiUpdate.Add("$dtId", myEventHubMes-sage.Properties["cloudEvents:subject"]);
                await out-putEvents.AddAsync(JsonConvert.SerializeObject(tsiUpdate));
            }
        }
    }
}

```

### Deploy Azure Function


# Create and connect a Time Series Insights instance

## Provision Time Series Insights
storage=adtholtsitorage$RANDOM
az storage account create -g $rgname -n $storage --https-only
key=$(az storage account keys list -g $rgname -n $storage --query [0].value --output tsv)
az timeseriesinsights environment longterm create -g $rgname -n $tsiname --location $location --sku-name L1 --sku-capacity 1 --data-retention 7 --time-series-id-properties "\$dtId" --storage-account-name $storage --storage-management-key $key

## Configure Event Hub as an Event Source

es_resource_id=$(az eventhubs eventhub show -n tsi-event-hub -g $rgname --namespace $dtname --query id -o tsv | sed -e 's/\r//g')
shared_access_key=$(az eventhubs namespace authorization-rule keys list -g $rgname --namespace-name $dtname -n RootManageSharedAccessKey --query primaryKey --output tsv | sed -e 's/\r//g')
az timeseriesinsights event-source eventhub create -g $rgname --environment-name $tsiname -n tsieh --key-name RootManageSharedAccessKey --shared-access-key $shared_access_key --event-source-resource-id $es_resource_id --consumer-group-name "\$Default"

## Configure permissions to access TSI environment
id=$(az ad user show --id teodelas@microsoft.com --query objectId -o tsv | sed -e 's/\r//g')
az timeseriesinsights access-policy create -g $rgname --environment-name $tsiname -n access1 --principal-object-id $id  --description "some description" --roles Contributor Reader

## View Data
Now, data should be flowing into your Time Series Insights instance, ready to be an-alyzed. Follow the steps below to explore the data coming in.

1.	Open your Time Series Insights instance in the [Azure portal](https://ms.portal.azure.com/#blade/HubsExtension/BrowseResourceBlade/resourceType/Microsoft.TimeSeriesInsights%2Fenvironments). 
1. Visit the Time Series Insights Explorer URL shown in the instance overview.
  
1.	In the explorer, you will see one Twin from Azure Digital Twins shown on the left. Select vibrationsensorxx, select vibration, and hit add.

1.	You should now be seeing the initial temperature readings from your vibra-tion sensor, as shown below. That same temperature reading is updated for sensor2 and machine1, and you can visualize those data streams in tan-dem

1.	If you allow the simulation to run for much longer, your visualization will look something like this:
 

