---
page_type: script
languages:
- powershell
products:
- azure
- azure-digital-twins
description: "How to deploy Azure Digital twins using automated powershell scripts"
---

# Use Automated scripts to deploy Azure Digital Twins
Deploy.ps1 is an Azure Digital Twins code sample used to deploy an Azure Digital Twins instance and set the required permissions. It can also be used as a starting point for writing your own scripted interactions.

## Prerequisites

* You'll need Azure subscription. You can set one up for free [here](https://azure.microsoft.com/free/?WT.mc_id=A261C142F).
* You'll need a resource group in your Azure subscription. You can create your resource group using [this](https://docs.microsoft.com/azure/azure-resource-manager/management/manage-resource-groups-cli) article.
* Download [Azure Digital Twins samples](https://docs.microsoft.com/samples/azure-samples/digital-twins-samples/digital-twins-samples/) to your machine. In the downloaded sample folder, the deployment script is located at _Azure_Digital_Twins_samples.zip > scripts > **deploy.ps1**_.

## Setup

1. Login to [Azure Cloud Shell](https://shell.azure.com) by using the following command

```azurecli
az login
```

2. Upload the *deploy.ps1* file that you have downloaded earlier from your machine to the Azure cloud shell by selecting *Upload/Download* icon in the navigation bar.

## Running the sample

deploy.ps1 script can be run in two modes.

* *.\deploy.ps1*
    This mode of the script:
    * Creates an instance
    * Sets up user access permissions
    * Sets access permissions for client applications
* *.\deploy.ps1 -endtoend*
    This mode of the script:
    * Creates digital twin endpoints
    * Creates twin routes

> [!NOTE]
> In Azure cloud shell window, make sure *select environment* dropdown in the navigation bar is set to *Powershell*

For more information on scripts, refer to [how-to-set-up-instance-scripted](https://docs.microsoft.com/azure/digital-twins/how-to-set-up-instance-scripted) article.
