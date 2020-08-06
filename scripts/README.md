---
page_type: script
languages:
- powershell
products:
- azure
- azure-digital-twins
description: "How to deploy Azure Digital Twins using automated Powershell scripts"
---

# Use Automated scripts to deploy Azure Digital Twins
Deploy.ps1 is an Azure Digital Twins code sample used to deploy an Azure Digital Twins instance and set the required permissions. It can also be used as a starting point for writing your own scripted interactions.

## Prerequisites

* You'll need an Azure subscription. You can set one up for free [here](https://azure.microsoft.com/free/?WT.mc_id=A261C142F).
* To be able to run the full script, you need to be classified as an Owner in your Azure subscription.
    * You can check your permission level by running this command in Cloud Shell:

```azurecli-interactive
az role assignment list --assignee <your-Azure-email>
```

If you are an owner, the `roleDefinitionName` value in the output is *Owner*:

![Screenshot of user checking role owner](/media/scripts/owner-role.png)

If you find that the value is *Contributor* or something other than *Owner*, you can proceed in one of the following ways:

* Contact your subscription Owner and request the Owner to run the script on your behalf.
* Contact either your subscription Owner or someone with User Access Admin role on the subscription, and request that they elevate you to Owner on the subscription so that you will have the permissions to proceed yourself. Whether this is appropriate depends on your organization and your role within it.

## Setup

1. Login to [Azure Cloud Shell](https://shell.azure.com) by using the following command

```azurecli
az login
```

2. Upload the *deploy.ps1* file that you have downloaded earlier from your machine to the Azure Cloud Shell by selecting *Upload/Download* icon in the navigation bar.

![Screenshot for uploading a file to Azure Cloud Shell](/media/scripts/cloud-shell-upload-file.png)

> [!NOTE]
> In the Azure Cloud Shell window, make sure *Select environment* dropdown in the navigation bar is set to *PowerShell*

## Running the sample

The deploy.ps1 script can be run in two modes.

* *.\deploy.ps1*
* *.\deploy.ps1 -endtoend*

> [!NOTE]
> When you run the script deploy.ps1, it stores the information in the config file. If you want to re-run the command for any of the following reasons, you will need to delete the config.json file and run the command again. You can do this by typing *rm config.json* file in your cloud shell.
> * Find errors in the script or the script is aborted
> * If you want to run the script for a different subscription
> * If you want to create new instances for a different resource group

![Screenshot of removing config file from the directory](/media/scripts/removing-config-file.png)

For more information on using this script to set up your Azure Digital Twins instance including verification steps, see [*How-to: Set up an instance and authentication (scripted)*](https://docs.microsoft.com/azure/digital-twins/how-to-set-up-instance-scripted) article.
