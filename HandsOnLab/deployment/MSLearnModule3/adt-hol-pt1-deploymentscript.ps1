[CmdletBinding()]
param(
    [Parameter(Mandatory=$True)]
    [string]$subscriptionID,

    [Parameter(Mandatory=$True)]
    [string]$username
)
Write-Host "NOTE!!Please make sure you're running the latest version of AZ CLI by running: az upgrade" -ForegroundColor Red
Write-Host "This script will deploy a new environment each time." -ForegroundColor DarkYellow



$azresult = (az account set -s $subscriptionID)
$azresult = (az account show --query '[id]' -o tsv)

if($azresult -eq $subscriptionID) {
    Write-Host "Subscription set to '$subscriptionID'" -ForegroundColor DarkGray
} else {
    throw "Unable to set subscription"

} 
$idresult = (az ad user show --id $username --query objectId -o tsv)

if ([string]::IsNullOrEmpty($idresult) -eq $false){
    Write-Host "User account is valid" -ForegroundColor DarkGray
} else {
    throw "Unable to find user account"
}


if (([string]::IsNullOrEmpty($idresult) -eq $false) -and ([string]::IsNullOrEmpty($azresult) -eq $false)){
$random = $(get-random -maximum 10000)
$rgname = "adtholrg"+ $random
$resourceprefix = "adthol" + $random
$dtname = $resourceprefix
$location = "eastus"
$functionstorage = $resourceprefix + "storage"
$telemetryfunctionname = $resourceprefix + "-telemetryfunction"
$twinupdatefunctionname = $resourceprefix + "-twinupdatefunction"

Write-Host "Deploying environment into Resource Group: $rgname" -ForegroundColor DarkGray
az extension add --name azure-iot --upgrade

az group create -n $rgname -l $location
az dt create --dt-name $dtname -g $rgname -l $location
Write-Host "Pausing for 60 seconds..." -ForegroundColor DarkYellow
Start-Sleep -Seconds 60
az dt role-assignment create -n $dtname -g $rgname --role "Azure Digital Twins Data Owner" --assignee $username -o json
$adthostname = "https://" + $(az dt show -n $dtname --query 'hostName' -o tsv)
Write-Host "Pausing for 120 seconds to allow permissions to propagate...."  -ForegroundColor DarkYellow
Start-Sleep -Seconds 120
#Add Modules to ADT
$factorymodelid = $(az dt model create -n $dtname --models ..\..\models\FactoryInterface.json --query [].id -o tsv)
$floormodelid = $(az dt model create -n $dtname --models ..\..\models\FactoryFloorInterface.json --query [].id -o tsv)
$prodlinemodelid = $(az dt model create -n $dtname --models ..\..\models\ProductionLineInterface.json --query [].id -o tsv)
$prodstepmodelid = $(az dt model create -n $dtname --models ..\..\models\ProductionStepInterface.json --query [].id -o tsv)
$gridingstepmodelid = $(az dt model create -n $dtname --models ..\..\models\ProductionStepGrinding.json --query [].id -o tsv)

#Instantiate ADT Instances
az dt twin create -n $dtname --dtmi $factorymodelid --twin-id "ChocolateFactory"
az dt twin create -n $dtname --dtmi $floormodelid --twin-id "FactoryFloor"
az dt twin create -n $dtname --dtmi $prodlinemodelid --twin-id "ProductionLine"
az dt twin create -n $dtname --dtmi $gridingstepmodelid --twin-id "GrindingStep"

#Create relationship
$relname = "rel_has_floors"
az dt twin relationship create -n $dtname --relationship $relname --twin-id "ChocolateFactory" --target "FactoryFloor" --relationship-id "Factory has floors"
$relname = "rel_runs_lines"
az dt twin relationship create -n $dtname --relationship $relname --twin-id "FactoryFloor" --target "ProductionLine" --relationship-id "Floor run production lines"
$relname = "rel_runs_steps"
az dt twin relationship create -n $dtname --relationship $relname --twin-id "ProductionLine" --target "GrindingStep" --relationship-id "Floor run production lines"
#Setup Azure Function
az storage account create --name $functionstorage --location $location --resource-group $rgname --sku Standard_LRS
az functionapp create --resource-group $rgname --consumption-plan-location $location --name $telemetryfunctionname --storage-account $functionstorage --functions-version 3
$principalID = $(az functionapp identity assign -g $rgname -n $telemetryfunctionname  --query principalId)
Write-Host "Pausing for 60 seconds..." -ForegroundColor DarkYellow
Start-Sleep -Seconds 60
az dt role-assignment create --dt-name $dtname --assignee $principalID --role "Azure Digital Twins Data Owner"
az functionapp config appsettings set -g $rgname -n $telemetryfunctionname --settings "ADT_SERVICE_URL=$adthostname "
Write-Host "Deploying code to function...this will take some time. " -ForegroundColor DarkYellow
Write-Host " Ignore warnings about SCM_DO_BUILD_DURING_DEPLOYMENT" -ForegroundColor DarkYellow
az functionapp deployment source config-zip -g $rgname -n $telemetryfunctionname --src ..\..\TwinInputFunction\twinfunction.zip

#Setup IoT Hub
az iot hub create --name $dtname --resource-group $rgname --sku S1 -l $location
Write-Host "Pausing for 2 minutes for IoT Hub to move to active state..." -ForegroundColor DarkYellow
Start-Sleep -Seconds 120

$iothub=$(az iot hub list -g $rgname --query [].id -o tsv)
$function=$(az functionapp function show -n $telemetryfunctionname -g $rgname --function-name twinsfunction --query id -o tsv)
az eventgrid event-subscription create --name 'IoTHubEvents' --source-resource-id $iothub --endpoint $function --endpoint-type azurefunction --included-event-types Microsoft.Devices.DeviceTelemetry

#Setup IoT Device
az iot hub device-identity create --device-id GrindingStep --hub-name $dtname -g $rgname
az iot hub device-identity connection-string show -d GrindingStep --hub-name $dtname
}
   
else {
 throw "Unable to run script" 
 }