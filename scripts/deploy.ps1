# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Unpublished works.
# --------------------------------------------------------------------------------------------

<#
    .SYNOPSIS
        Example script for creating an ADT instance.

    .DESCRIPTION
        Example script for creating an ADT instance.

        Prior to running, a user will have to install Azure CLI from https://aka.ms/azure-cli

    .PARAMETER endToEnd

    .PARAMETER RegisterAadApp
        While creating the ADT instance this script can optionally create an Azure Active Directory app registration that can be used to enable authentication for various types of applications.

    .EXAMPLE
        ./deploy.ps1

        Create an Azure Digital Twins instance. User will be prompted for any required information not present in ./config.json.

    .EXAMPLE
        ./deply.ps1 -endToEnd

        Create an Azure Digital Twins instance as well as Azure Function and Event Grid resources. Create an endpoint and route in Azure Digital Twins instance for the egress of data. User will be prompted for any required information not present in ./config.json.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$False)]
    [switch]$endToEnd = $False,

    [Parameter(Mandatory=$False)]
    [switch]$RegisterAadApp
)

function Write-ManifestFile {
    $manifest = [ordered]@{
        resourceAppId = "https://digitaltwins.azure.net"
        resourceAccess = @{
            id = "4589bd03-58cb-4e6c-b17f-b580e39652f8"
            type = "Scope"
        }
    }
    $manifest | ConvertTo-Json -Depth 100 | Out-File "manifest.json"
}

$env:AZURE_CORE_NO_COLOR='True'
$cooloff = 20
$fileCooloff = 2

#read config
$configFile = "config.json"
$config = "{}" | ConvertFrom-Json
if([System.IO.File]::Exists($configFile)) {
    $config = Get-Content $configFile | ConvertFrom-Json
    # Write-Host "Configuration read from JSON: $config" -ForegroundColor DarkGray
}

Write-Host "Checking for legacy azure-cli-iot-ext..." -ForegroundColor DarkGray
$azresult = (az extension show --name azure-cli-iot-ext --query name -o json 2>$null) | ConvertFrom-Json
if (!$azresult) {
    Write-Host "Legacy IoT CLI extension not found, continuing..." -ForegroundColor DarkGray
} else {
    Write-Host "Attempting to remove legacy IoT CLI extension: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az extension remove --name azure-cli-iot-ext'" -ForegroundColor Yellow
    $azresult = (az extension remove --name azure-cli-iot-ext -o json --only-show-errors 2>$null) | ConvertFrom-Json
    if(!$azresult) {
        throw "Unable to remove azure-cli-iot-ext"
    }
}

Write-Host "Ensuring latest Azure IoT CLI is installed" -ForegroundColor DarkGray
$azresult = (az extension show --name azure-iot --query version -o json --only-show-errors 2>$null) | ConvertFrom-Json
if ($azresult) {
    Write-Host "Removing existing azure-iot CLI extension: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az extension remove --name azure-iot'" -ForegroundColor Yellow
    $azresult = (az extension remove --name azure-iot -o json --only-show-errors 2>$null) | ConvertFrom-Json
    if($azresult) {
        throw "Unable to remove azure-iot CLI extension"
    }
}

Write-Host "Installing latest azure-iot CLI extension: " -ForegroundColor DarkGray -NoNewline
Write-Host "'az extension add --name azure-iot'" -ForegroundColor Yellow
$azresult = (az extension add --name azure-iot -o json --only-show-errors 2>$null) | ConvertFrom-Json
if($azresult) {
    throw "Error installing azure-iot CLI extension"
}

# remove any old version of EventGrid extension
Write-Host "Removing any existing EventGrid CLI extension: " -ForegroundColor DarkGray -NoNewline
Write-Host "'az extension remove -n eventgrid'" -ForegroundColor Yellow
(az extension remove -n eventgrid -o json --only-show-errors 2>$null)

#install latest version of eventgrid extension
Write-Host "Installing the latest EventGrid CLI extension: " -ForegroundColor DarkGray -NoNewline
Write-Host "'az extension add -n eventgrid'" -ForegroundColor Yellow
(az extension add -n eventgrid -o json --only-show-errors 2>$null)

#set subscription
$subscription = ""
if (Get-Member -InputObject $config -Name "subscription" -MemberType Properties) {
    $subscription = $config.subscription
} else {
    $config | Add-Member -Name "subscription" -Value "" -MemberType NoteProperty
}

while ([string]::IsNullOrEmpty($subscription)) {
    $subscription = Read-Host "Please specify your Azure subscription id"
}
Write-Host "Setting active subscription: " -ForegroundColor DarkGray -NoNewline
Write-Host "'az account set -s $subscription'" -ForegroundColor Yellow
$azresult = (az account set -s $subscription) | ConvertFrom-Json

$azresult = (az account show --query '[id, name]' -o json --only-show-errors) | ConvertFrom-Json

if($azresult -and ($azresult[0] -eq $subscription -or $azresult[1] -eq $subscription)) {
    $subscription = $azresult[0]
    $config.subscription = $subscription
    Write-Host "Subscription set to '$subscription'" -ForegroundColor DarkGray
} else {
    throw "Unable to set subscription"
}
$config | ConvertTo-Json -Depth 100 | Out-File $configFile

#get tenant
$tenantId = ""

if (Get-Member -InputObject $config -Name "tenant_id" -MemberType Properties) {
    $tenantId = $config.tenant_id
} else {
    $config | Add-Member -Name "tenant_id" -Value "" -MemberType NoteProperty
}

while ([string]::IsNullOrEmpty($tenantId)) {
    Write-Host "Getting tenant: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az account show --query tenantId'" -ForegroundColor Yellow
    $tenantId = (az account show --query tenantId) | ConvertFrom-Json
}

$config.tenant_id = $tenantId
$config | ConvertTo-Json -Depth 100 | Out-File $configFile

#get location
$location = ""

if (Get-Member -InputObject $config -Name "location" -MemberType Properties) {
    $location = $config.location
} else {
    $config | Add-Member -Name "location" -Value "" -MemberType NoteProperty
}
# get locations (Display Names) where Azure Digital Twins is enabled
$twin_locations = ((az provider show -n Microsoft.DigitalTwins --query "resourceTypes[?resourceType=='digitalTwinsInstances'].locations" -o json) | ConvertFrom-Json)
# get all locations available in subscription and filter to values with display name in twin locations. This allows for westus2 instead of escaping "West US 2" throughout the script
$account_locations = (az account list-locations -o json | ConvertFrom-Json)

$valid_locations = [System.Linq.Enumerable]::ToList([System.Linq.Enumerable]::Where($account_locations, [Func[object, bool]] {param($x) ([System.Linq.Enumerable]::Any($twin_locations, [Func[object, bool]] {param($y) $y -eq $x.displayName}))}))
if(![string]::IsNullOrWhiteSpace($location)) {
    $valid_location = [System.Linq.Enumerable]::FirstOrDefault($valid_locations, [Func[object, bool]] { param($x) $x.name -eq $location -or $x.displayName -like $location})
    if($valid_location) {
        $location = $valid_location.name
    } else {
        Write-Host "Location '$location' is not an Azure Digital Twins enabled location. Clearing input." -ForegroundColor Red
        $location = ""
    }
}

while ([string]::IsNullOrWhiteSpace($location)) {
    $location = Read-Host "Please specify a valid Azure Digital Twins-enabled location for your solution"
    # check if valid location
    $valid_location = [System.Linq.Enumerable]::FirstOrDefault($valid_locations, [Func[object, bool]] { param($x) $x.name -eq $location -or $x.displayName -like $location})
    if($valid_location) {
        $location = $valid_location.name
    } else {
        Write-Host "Location '$location' is not an Azure Digital Twins enabled location. Clearing input." -ForegroundColor Red
        $location = ""
    }
    # check if we set location
    if ([string]::IsNullOrWhiteSpace($location)) {
        Write-Host "Valid locations include:"
        foreach ($location_item in $valid_locations) {
            Write-Host $location_item.name -ForegroundColor Blue
        }
    }
}

$config.location = $location
$config | ConvertTo-Json -Depth 100 | Out-File $configFile

#get resource group
$resource_group = ""

if (Get-Member -InputObject $config -Name "resource_group" -MemberType Properties) {
    $resource_group = $config.resource_group
} else {
    $config | Add-Member -Name "resource_group" -Value "" -MemberType NoteProperty
}

while ([string]::IsNullOrEmpty($resource_group)) {
    $resource_group = Read-Host "Please specify your Azure resource group"
}

$config.resource_group = $resource_group

Write-Host "Checking for resource group: " -ForegroundColor DarkGray -NoNewline
Write-Host "'az group show --name $resource_group'" -ForegroundColor Yellow
$azresult = (az group show --name $resource_group --query properties.provisioningState -o json --only-show-errors 2>$null) | ConvertFrom-Json
if(!$azresult) {
    Write-Host "Creating resource group: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az group create --name $resource_group --location $location'" -ForegroundColor Yellow
    $azresult = (az group create --name $resource_group --location $location --only-show-errors -o json 2>$null) | ConvertFrom-Json
    if (!$?) {
        throw $azresult
    }
} elseif ("Succeeded" -ne $azresult) {
    Write-Host "Resource group $resource_group exists but isn't fully provisioned waiting for $cooloff seconds" -ForegroundColor DarkGray
    Start-Sleep -Seconds $cooloff
}
$config | ConvertTo-Json -Depth 100 | Out-File $configFile

# register namespace
$azresult = (az provider show -n Microsoft.DigitalTwins -o json --only-show-errors 2>$null) | ConvertFrom-Json
if(!$azresult) {
    Write-Host "Registering Azure Digital Twins resource provider: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az provider register --namespace 'Microsoft.DigitalTwins''" -ForegroundColor Yellow
    (az provider register --namespace "Microsoft.DigitalTwins" --only-show-errors)
}

#get name
$name = ""
if (Get-Member -InputObject $config -Name "name" -MemberType Properties) {
    $name = $config.name
} else {
    $config | Add-Member -Name "name" -Value "" -MemberType NoteProperty
}

while ([string]::IsNullOrEmpty($name)) {
    $name = Read-Host "Please specify your ADT instance name"
}
$config.name = $name

#create adt instance
$azresult = (az dt show --dt-name $name --query provisioningState -o json --only-show-errors 2>$null) | ConvertFrom-Json
if(!$azresult) {
    Write-Host "Creating Azure Digital Twins resource: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az dt create --dt-name $name -g $resource_group -l $location'" -ForegroundColor Yellow
    $azresult = az dt create --dt-name $name -g $resource_group -l $location --only-show-errors -o json | ConvertFrom-Json
    if (!$?) {
        throw $azresult
    }
    Write-Host "Waiting on Digital Twins post-provisioning" -ForegroundColor DarkGray
    Start-Sleep -Seconds $cooloff
} elseif ("Succeeded" -ne $azresult) {
    Write-Host "Azure Digital Twin instance $name has not completed provisioning waiting for $cooloff seconds" -ForegroundColor DarkGray
    Start-Sleep -Seconds $cooloff
}
$config | ConvertTo-Json -Depth 100 | Out-File $configFile

#get hostname
$hostname = (az dt show --dt-name $name --only-show-errors | ConvertFrom-Json)
if (Get-Member -InputObject $config -Name "hostname" -MemberType Properties) {
    $config.hostname = $hostname
} else {
    $config | Add-Member -Name "hostname" -Value $hostname -MemberType NoteProperty
}

#assign user role
Write-Host "Querying CLI user..." -ForegroundColor DarkGray
$cliUser = az account list --query "[?isDefault && state == 'Enabled'] | [0].user.name" 2>$null
Write-Host "Found $cliUser. Setting assignee $cliUser as owner: " -ForegroundColor DarkGray -NoNewline
Write-Host "'az dt role-assignment create -n $name -g $resource_group --role ""Azure Digital Twins Owner (Preview)"" --assignee $cliUser'" -ForegroundColor Yellow
$result = (az dt role-assignment create -n $name -g $resource_group --role "Azure Digital Twins Owner (Preview)" --assignee $cliUser -o json --only-show-errors 2>$null) | ConvertFrom-Json
if ($result){
    Write-Host "Waiting $cooloff seconds for propagation..." -ForegroundColor DarkGray
    Start-Sleep -Seconds $cooloff
}
Write-Host "Found $cliUser. Querying user objectId..." -ForegroundColor DarkGray
$userObjectId =  az ad user show --id $cliUser --query objectId 2>$null

Write-Host "Found $userObjectId. Setting assignee $userObjectId as owner: " -ForegroundColor DarkGray -NoNewline
Write-Host "'az dt role-assignment create -n $name -g $resource_group --role ""Azure Digital Twins Owner (Preview)"" --assignee $userObjectId'" -ForegroundColor Yellow
$result = (az dt role-assignment create -n $name -g $resource_group --role "Azure Digital Twins Owner (Preview)" --assignee $userObjectId -o json --only-show-errors 2>$null) | ConvertFrom-Json

if ($result){
    Write-Host "Waiting $cooloff seconds for propagation..." -ForegroundColor DarkGray
    Start-Sleep -Seconds $cooloff
}

if ($registerAadApp) {
    #create aad app
    $display_name = ""
    if (Get-Member -InputObject $config -Name "display_name" -MemberType Properties) {
        $display_name = $config.display_name
    } else {
        $config | Add-Member -Name "display_name" -Value "" -MemberType NoteProperty
    }

    while ([string]::IsNullOrEmpty($display_name)) {
        $display_name = Read-Host "Please specify your AAD application display name"
    }
    $config.display_name = $display_name

    Write-Host "Searching for existing Azure Active Directory application: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az ad app list --display-name $display_name'" -ForegroundColor Yellow
    $application_id = (az ad app list --display-name $display_name --query '[0].appId' -o json --only-show-errors 2>$null) | ConvertFrom-Json
    if(!$application_id) {
        # we need to create it
        Write-ManifestFile
        $reply_url = ""
        if (Get-Member -InputObject $config -Name "reply_url" -MemberType Properties) {
            $reply_url = $config.reply_url
        } else {
            $config | Add-Member -Name "reply_url" -Value "" -MemberType NoteProperty
        }
        while ([string]::IsNullOrEmpty($reply_url)) {
            $reply_url = Read-Host "Please specify your AAD application reply url"
        }
        $config.reply_url = $reply_url

        Write-Host "Creating AAD application registration: " -ForegroundColor DarkGray -NoNewline
        Write-Host "'az ad app create --display-name $display_name --native-app --required-resource-accesses ./manifest.json --reply-url $reply_url'" -ForegroundColor Yellow
        (az ad app create --display-name $display_name --native-app --required-resource-accesses ./manifest.json --reply-url $reply_url -o json --only-show-errors 2>$null)
    }
    $config | ConvertTo-Json -Depth 100 | Out-File $configFile
}

if ($endToEnd) {
    #create iot hub
    $iot_hub = ""
    if (Get-Member -InputObject $config -Name "iot_hub" -MemberType Properties) {
        $iot_hub = $config.iot_hub
    } else {
        $config | Add-Member -Name "iot_hub" -Value "" -MemberType NoteProperty
    }
    while ([string]::IsNullOrEmpty($iot_hub)) {
        $iot_hub = Read-Host "Please specify an IoT Hub name to use"
    }
    $config.iot_hub = $iot_hub
    # check for hub already exists
    $azresult = (az iot hub show --name $iot_hub --query name -o json --only-show-errors 2>$null) | ConvertFrom-Json
    if(!$azresult) {
        Write-Host "Creating IoT Hub: " -ForegroundColor DarkGray -NoNewline
        Write-Host "'az iot hub create --name $iot_hub -g $resource_group --sku S1'" -ForegroundColor Yellow
        $azresult = (az iot hub create --name $iot_hub -g $resource_group --sku S1 -o json --only-show-errors) | ConvertFrom-Json
        if($? -and !$azresult) {
            throw "Unable to create IoT Hub"
        }
    }
    $config | ConvertTo-Json -Depth 100 | Out-File $configFile

    #create event grid topic
    $topic_name = ""
    if (Get-Member -InputObject $config -Name "topic_name" -MemberType Properties) {
        $topic_name = $config.topic_name
    } else {
        $config | Add-Member -Name "topic_name" -Value "" -MemberType NoteProperty
    }
    while ([string]::IsNullOrEmpty($topic_name)) {
        $topic_name = Read-Host "Please specify an EventGrid topic"
    }
    $config.topic_name = $topic_name

    $azresult = (az eventgrid topic show --name $topic_name --resource-group $resource_group --query name -o json --only-show-errors 2>$null) | ConvertFrom-Json
    if(!$azresult) {
        Write-Host "Creating EventGrid topic: " -ForegroundColor DarkGray -NoNewline
        Write-Host "'az eventgrid topic create -g $resource_group --name $topic_name -l $location'" -ForegroundColor Yellow
        $azresult = (az eventgrid topic create -g $resource_group --name $topic_name -l $location -o json --only-show-errors) | ConvertFrom-Json
        if($? -and !$azresult) {
            throw "Unable to create EventGrid topic"
        }
    }
    $config | ConvertTo-Json -Depth 100 | Out-File $configFile

    #create event grid endpoint
    $endpoint_name = ""
    if(Get-Member -InputObject $config -Name "endpoint_name" -MemberType Properties) {
        $endpoint_name = $config.endpoint_name
    } else {
        $config | Add-Member -Name "endpoint_name" -Value "" -MemberType NoteProperty
    }
    while ([string]::IsNullOrEmpty($endpoint_name)) {
        $endpoint_name = Read-Host "Please specify a name for the Digital Twin endpoint"
    }
    $config.endpoint_name = $endpoint_name
    $azresult = (az dt endpoint show --dtn $name -g $resource_group --en $endpoint_name --query name -o json --only-show-errors 2>$null) | ConvertFrom-Json
    if(!$azresult) {
        Write-Host "Creating endpoint on Digital Twins instance: " -ForegroundColor DarkGray -NoNewline
        Write-Host "'az dt endpoint create eventgrid --dtn $name -g $resource_group --en $endpoint_name --egg $resource_group --egt $topic_name'" -ForegroundColor Yellow
        $azresult = (az dt endpoint create eventgrid --dtn $name -g $resource_group --en $endpoint_name --egg $resource_group --egt $topic_name -o json --only-show-errors) | ConvertFrom-Json

        if($? -and !$azresult) {
            throw "Unable to create endpoint"
        }
        Write-Host "Waiting for endpoint post provisioning" -ForegroundColor DarkGray
        Start-Sleep -Seconds $cooloff
    }
    $config | ConvertTo-Json -Depth 100 | Out-File $configFile

    #add route to dt instance
    $route_name = ""
    if(Get-Member -InputObject $config -Name "route_name" -MemberType Properties) {
        $route_name = $config.route_name
    } else {
        $config | Add-Member -Name "route_name" -Value "" -MemberType NoteProperty
    }
    while ([string]::IsNullOrEmpty($route_name)) {
        $route_name = Read-Host "Please specify a name for the Digital Twin route"
    }
    $config.route_name = $route_name
    $azresult = (az dt route show --dtn $name -g $resource_group --rn $route_name --query id -o json --only-show-errors 2>$null) | ConvertFrom-Json
    if(!$azresult) {
        Write-Host "Creating route on Digital Twins instance: " -ForegroundColor DarkGray -NoNewline
        Write-Host "'az dt route create --dtn $name --endpoint-name $endpoint_name --route-name $route_name'" -ForegroundColor Yellow
        $azresult = (az dt route create --dtn $name --endpoint-name $endpoint_name --route-name $route_name -o json --only-show-errors) | ConvertFrom-Json

        if($? -and !$azresult) {
            Write-Host "Unable to create route." -ForegroundColor Red
            Write-Host "Possible causes are using CloudShell or a failure to assign permissions using 'az dt role-assignment' and requires granting your user Azure Digital Twins Owner role to the Digital Twins resource using Azure Portal instead"
            $continue = Read-Host "Do you wish to continue script execution and create route manually (y/n)?"
            if($continue -ne "y") {
                throw "Terminating script"
            }
        }
    }
    $config | ConvertTo-Json -Depth 100 | Out-File $configFile

    #create storage account
    $storage_account = ""
    if(Get-Member -InputObject $config -Name "storage_account" -MemberType Properties) {
        $storage_account = $config.storage_account
    } else {
        $config | Add-Member -Name "storage_account" -Value "" -MemberType NoteProperty
    }
    while ([string]::IsNullOrEmpty($storage_account)) {
        $storage_account = Read-Host "Please specify a storage account name to be used for Azure Functions"
    }
    $config.storage_account = $storage_account
    $azresult = (az storage account show --name $storage_account --query id -o json --only-show-errors 2>$null) | ConvertFrom-Json
    if(!$azresult) {
        Write-Host "Creating storage account: " -ForegroundColor DarkGray -NoNewline
        Write-Host "'az storage account create -n $storage_account -g $resource_group -l $location --sku Standard_LRS'" -ForegroundColor Yellow
        $azresult = (az storage account create -n $storage_account -g $resource_group -l $location --sku Standard_LRS -o json --only-show-errors) | ConvertFrom-Json

        if($? -and !$azresult) {
            throw "Unable to create storage account"
        }
    }
    $config | ConvertTo-Json -Depth 100 | Out-File $configFile

    #create function app
    $function_app = ""
    if(Get-Member -InputObject $config -Name "function_app" -MemberType Properties) {
        $function_app = $config.function_app
    } else {
        $config | Add-Member -Name "function_app" -Value "" -MemberType NoteProperty
    }
    while ([string]::IsNullOrEmpty($function_app)) {
        $function_app = Read-Host "Please specify an Azure Function name"
    }
    $config.function_app = $function_app
    $azresult = (az functionapp show --name $function_app --resource-group $resource_group --query name -o json --only-show-errors 2>$null) | ConvertFrom-Json
    if(!$azresult) {
            Write-Host "Creating Azure Function: " -ForegroundColor DarkGray -NoNewline
            Write-Host "'az functionapp create --consumption-plan-location $location --name $function_app --os-type Windows --resource-group $resource_group --runtime dotnet --storage-account $storage_account'" -ForegroundColor Yellow
            $azresult = (az functionapp create --consumption-plan-location $location --name $function_app --os-type Windows --resource-group $resource_group --runtime dotnet --storage-account $storage_account -o json --only-show-errors) | ConvertFrom-Json

            if($? -and !$azresult) {
                throw "Unable to create Azure Function"
            }
    }
    $config | ConvertTo-Json -Depth 100 | Out-File $configFile

    #assign function identity
    Write-Host "Creating system-managed identity for Azure Function: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az functionapp identity assign -g $resource_group -n $function_app'" -ForegroundColor Yellow
    $azresult = (az functionapp identity assign -g $resource_group -n $function_app -o json --only-show-errors) |ConvertFrom-Json
    if($? -and !$azresult) {
        throw "Unable to create system-managed identity for Azure Function"
    }
    if(Get-Member -InputObject $azresult -Name "principalId" -MemberType Properties) {
        $function_principal = $azresult.principalId
        Write-Host "Assigning Function App identity the role of owner to Digital Twins instance: " -ForegroundColor DarkGray -NoNewline
        Write-Host "'az dt role-assignment create --assignee $function_principal --dtn $name --role ""Azure Digital Twins Owner (Preview)""'" -ForegroundColor Yellow
        $azresult = (az dt role-assignment create --assignee $function_principal --dtn $name --role "Azure Digital Twins Owner (Preview)" -o json --only-show-errors 2>$null) | ConvertFrom-Json
    } else {
        throw "Unable to find principalId for Azure Function identity"
    }

    #get ingress function name
    $ingress_function = ""
    if(Get-Member -InputObject $config -Name "ingress_function" -MemberType Properties) {
        $ingress_function = $config.ingress_function
    } else {
        $config | Add-Member -Name "ingress_function" -Value "" -MemberType NoteProperty
    }
    while ([string]::IsNullOrEmpty($ingress_function)) {
        $ingress_function = Read-Host "Please specify the function name for Digital Twin data ingress from IoT Hub/Event Grid"
    }
    $config.ingress_function = $ingress_function
    $config | ConvertTo-Json -Depth 100 | Out-File $configFile

    #get processing function name
    $processing_function = ""
    if(Get-Member -InputObject $config -Name "processing_function" -MemberType Properties) {
        $processing_function = $config.processing_function
    } else {
        $config | Add-Member -Name "processing_function" -Value "" -MemberType NoteProperty
    }
    while ([string]::IsNullOrEmpty($processing_function)) {
        $processing_function = Read-Host "Please specify the function name for processing Digital Twins change events"
    }
    $config.processing_function = $processing_function
    $config | ConvertTo-Json -Depth 100 | Out-File $configFile

    # try to retrieve the system key to create the eventgrid subscriptions
    if(Get-Member -InputObject $config -Name "function_code" -MemberType Properties) {
        $function_code = $config.function_code
    } else {
        $function_code = (az rest --method post --uri "/subscriptions/$subscription/resourceGroups/$resource_group/providers/Microsoft.Web/sites/$function_app/host/default/listKeys?api-version=2018-02-01" --query systemKeys.eventgrid_extension -o json --only-show-errors 2>$null) | ConvertFrom-Json
        $config | Add-Member -Name "function_code" -Value $function_code -MemberType NoteProperty
    }
    if(!$function_code){
        Write-Host "Unable to find the eventgrid_extension system key used for the ingress/egress functions. Ensure they have been deployed and retrieve the value from the Azure portal." -ForegroundColor Red
        while (!$function_code) {
            $function_code = Read-Host "Please specify the value for 'eventgrid_extension' system key"
        }
        $config.function_code = $function_code
    }

    $iot_hub_id = (az iot hub show --name $iot_hub --query id -o json --only-show-errors 2>$null) | ConvertFrom-Json
    $function_host = (az functionapp show --name $function_app -g $resource_group --query defaultHostName -o json --only-show-errors 2>$null) |ConvertFrom-Json
    Write-Host "Checking for existing ingress event subscription: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az eventgrid event-subscription list --source-resource-id $iot_hub_id'" -ForegroundColor Yellow
    $azresult = (az eventgrid event-subscription list --source-resource-id $iot_hub_id --only-show-errors -o json) | ConvertFrom-Json
    if(!$azresult -or ![System.Linq.Enumerable]::FirstOrDefault($azresult, [Func[object, bool]] { param($x) $ingress_function -eq $x.name})) {
        $ingress_endpoint = "https://$function_host/runtime/webhooks/EventGrid?functionName=$ingress_function" + "&" + "code=$function_code"
        $ingress_subscription_url = "$iot_hub_id/providers/Microsoft.EventGrid/eventSubscriptions/$ingress_function"
        $body = "{""name"": ""$ingress_function"",""properties"": {""topic"": ""$iot_hub_id"",""destination"": {""endpointType"": ""WebHook"",""properties"": {""endpointUrl"": ""$ingress_endpoint"",""maxEventsPerBatch"": 1,""preferredBatchSizeInKilobytes"": 64}},""filter"": {""includedEventTypes"": [""Microsoft.Devices.DeviceTelemetry""]},""eventDeliverySchema"": ""EventGridSchema""}}" | ConvertTo-Json
        $body | ConvertFrom-Json | Out-File "ingressbody.json"

        $parameters = "{""api-version"": ""2020-04-01-preview""}"
        $parameters | Out-File "ingressparameters.json"

        Write-Host "Creating ingress Event Grid subscription: " -ForegroundColor DarkGray -NoNewline
        Write-Host "'az rest --method put --uri $ingress_subscription_url -b ""@ingressbody.json"" --uri-parameters ""@ingressparameters.json""'" -ForegroundColor Yellow
        Start-Sleep -Seconds $fileCooloff

        $azresult = (az rest --method put --uri $ingress_subscription_url -b "@ingressbody.json" --uri-parameters "@ingressparameters.json" -o json) | ConvertFrom-Json
        if(!$azresult) {
            throw "Unable to create Event Grid ingress subscription"
        } else {
            Write-Host "Created processing subscription: $azresult" -ForegroundColor DarkGray
        }
    }

    $eventgrid_id = (az eventgrid topic show -g $resource_group -n $topic_name --only-show-errors --query id -o json 2>$null) | ConvertFrom-Json
    Write-Host "Checking for existing processing event subscription: " -ForegroundColor DarkGray -NoNewline
    Write-Host "'az eventgrid event-subscription list --source-resource-id $eventgrid_id" -ForegroundColor Yellow
    $azresult = (az eventgrid event-subscription list --source-resource-id $eventgrid_id --only-show-errors -o json) | ConvertFrom-Json
    if(!$azresult -or ![System.Linq.Enumerable]::FirstOrDefault($azresult, [Func[object, bool]] { param($x) $processing_function -eq $x.name})) {

        $function_id = (az functionapp show -g $resource_group -n $function_app --only-show-errors --query id -o json 2>$null) | ConvertFrom-Json
        $full_function_id = "$function_id/functions/$processing_function"
        $body = "{""name"": ""$processing_function"",""properties"": {""topic"": ""$eventgrid_id"",""destination"": {""endpointType"": ""AzureFunction"",""properties"": {""resourceId"": ""$full_function_id"",""maxEventsPerBatch"": 1,""preferredBatchSizeInKilobytes"": 64}},""eventDeliverySchema"": ""EventGridSchema""}}" | ConvertTo-Json
        $body | ConvertFrom-Json | Out-File "proccessingbody.json"

        $parameters = "{""api-version"": ""2020-04-01-preview""}"
        $parameters | Out-File "processingparameters.json"

        $eventgrid_subscription_url = "$eventgrid_id/providers/Microsoft.EventGrid/eventSubscriptions/$processing_function"

        Write-Host "Creating processsing Event Grid subscription: " -ForegroundColor DarkGray -NoNewline
        Write-Host "'az rest --method put --uri ""$eventgrid_subscription_url"" -b ""@proccessingbody.json"" --uri-parameters ""@processingparameters.json""'" -ForegroundColor Yellow
        Start-Sleep -Seconds $fileCooloff

        $azresult = (az rest --method put --uri "$eventgrid_subscription_url" -b "@proccessingbody.json" --uri-parameters "@processingparameters.json" -o json) | ConvertFrom-Json
        if(!$azresult) {
            throw "Unable to create Event Grid processing subscription"
        } else {
            Write-Host "Created processing subscription: $azresult" -ForegroundColor DarkGray
        }
    }
}
# Write the config when done
$config | ConvertTo-Json -Depth 100 | Out-File $configFile
Write-Host "Deployment completed successfully" -ForegroundColor Green