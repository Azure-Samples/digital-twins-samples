dtname=$1
rgname=$2
prefix=$3
location=$4
ehnamespace=$5
twinseventhub=$6
twinsehauth=$7
iothubname=$8
tsiname=$9
id=$10
git clone https://github.com/Teodelas/digital-twins-samples.git -q
az extension add --name azure-iot -y
factorymodelid=$(az dt model create -n $dtname --models /mnt/azscripts/azscriptinput/digital-twins-samples/HandsOnLab/models/FactoryInterface.json --query [].id -o tsv)
floormodelid=$(az dt model create -n $dtname --models /mnt/azscripts/azscriptinput/digital-twins-samples/HandsOnLab/models/FactoryFloorInterface.json --query [].id -o tsv)
prodlinemodelid=$(az dt model create -n $dtname --models /mnt/azscripts/azscriptinput/digital-twins-samples/HandsOnLab/models/ProductionLineInterface.json --query [].id -o tsv)
prodstepmodelid=$(az dt model create -n $dtname --models /mnt/azscripts/azscriptinput/digital-twins-samples/HandsOnLab/models/ProductionStepInterface.json --query [].id -o tsv)
gridingstepmodelid=$(az dt model create -n $dtname --models /mnt/azscripts/azscriptinput/digital-twins-samples/HandsOnLab/models/ProductionStepGrinding.json --query [].id -o tsv)

      #Instantiate ADT Instances
  az dt twin create -n $dtname --dtmi $factorymodelid --twin-id 'ChocolateFactory'
  az dt twin create -n $dtname --dtmi $floormodelid --twin-id 'FactoryFloor'
  az dt twin create -n $dtname --dtmi $prodlinemodelid --twin-id 'ProductionLine'
  az dt twin create -n $dtname --dtmi $gridingstepmodelid --twin-id 'GrindingStep'

  #Create relationship
  relname='rel_has_floors'
  az dt twin relationship create -n $dtname --relationship $relname --twin-id 'ChocolateFactory' --target 'FactoryFloor' --relationship-id 'Factory has floors'
  relname='rel_runs_lines'
  az dt twin relationship create -n $dtname --relationship $relname --twin-id 'FactoryFloor' --target 'ProductionLine' --relationship-id 'Floor run production lines'
  relname='rel_runs_steps'
  az dt twin relationship create -n $dtname --relationship $relname --twin-id 'ProductionLine' --target 'GrindingStep' --relationship-id 'Floor run production lines'
  
  az dt endpoint create eventhub --endpoint-name EHEndpoint --eventhub-resource-group $rgname --eventhub-namespace $ehnamespace --eventhub $twinseventhub --eventhub-policy $twinsehauth -n $dtname
  az dt route create -n $dtname --endpoint-name EHEndpoint --route-name EHRoute --filter "type = 'Microsoft.DigitalTwins.Twin.Update'"
  sleep 30
  az extension add --name timeseriesinsights -y
  az timeseriesinsights access-policy create -g $rgname --environment-name $tsiname -n access1 --principal-object-id $id  --description "some description" --roles Contributor Reader
  az iot hub device-identity create --device-id GrindingStep --hub-name $iothubname -g $rgname
  connectionstring=$(az iot hub device-identity connection-string show -d GrindingStep --hub-name $iothubname -o tsv)

  result="{\"device-connection-string\":"\""'$connectionstring'"\""}"
  echo $result | jq -c > $AZ_SCRIPTS_OUTPUT_PATH