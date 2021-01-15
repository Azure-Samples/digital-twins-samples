dtname=$1
rgname=$2
prefix=$3
location=$4
userid=$5
git clone https://github.com/Azure-Samples/digital-twins-samples.git -q
az extension add --name azure-iot --upgrade
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
  result="{\"PS-Script\":"\"" start-copy-> \$random = '$prefix'; \$rgname = '$rgname';\$location = '$location';\$dtname = '$dtname';\$functionstorage = '${prefix}storage';\$telemetryfunctionname = '${prefix}-telemetryfunction';\$twinupdatefunctionname = '${prefix}-twinupdatefunction'; \$id= '$userid'; \$username = 'replaceme@contoso.com' <-endcopy "\""}"
  echo $result | jq -c > $AZ_SCRIPTS_OUTPUT_PATH