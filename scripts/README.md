---
page_type: script
languages:
- powershell
products:
- azure
- azure-digital-twins
description: Script for deploying Azure Digital Twins and associated resources using automated Powershell script
---

# Use automated script to deploy Azure Digital Twins

*Deploy.ps1* is an Azure Digital Twins code sample that can be used to deploy an Azure Digital Twins instance and grant the current user the _Azure Digital Twins Owner (Preview)_ management role on the instance. It can also be used to set up an Azure AD app registration, and additional Azure resources to be used along with Azure Digital Twins in an end-to-end solution.

You can use the script directly to deploy resources, or reference it as a starting point for writing your own scripted interactions.

### Table of contents
* [Prerequisites](#prerequisites)
* [Running the sample](#running-the-sample)
* [Re-running the sample](#re-running-the-sample)
* [Known issues](#known-issues)
* [Next steps](#next-steps)

## Prerequisites

You'll need an Azure subscription. You can set one up for free [here](https://azure.microsoft.com/free/?WT.mc_id=A261C142F).

### Permission requirements

To be able to run the full script, you need to have a [role in your subscription](https://docs.microsoft.com/azure/role-based-access-control/rbac-and-directory-admin-roles) that has the following permissions:
* Create and manage Azure resources
* Manage user access to Azure resources (including granting and delegating permissions)

Common roles that meet this requirement are *Owner*, *Account admin*, or the combination of *User Access Administrator* and *Contributor*. For a complete explanation of roles and permissions, including what permissions are included with other roles, visit [*Classic subscription administrator roles, Azure roles, and Azure AD roles*](https://docs.microsoft.com/azure/role-based-access-control/rbac-and-directory-admin-roles) in the Azure RBAC documentation.

To view your role in your subscription, visit the [subscriptions page](https://portal.azure.com/#blade/Microsoft_Azure_Billing/SubscriptionsBlade) in the Azure portal (you can use this link or look for *Subscriptions* with the portal search bar). Look for the name of the subscription you are using, and view your role for it in the *My role* column:

![View of the Subscriptions page in the Azure portal, showing user as an owner](../media/scripts/subscriptions-role.png)

If you find that the value is *Contributor*, or another role that doesn't have the required permissions described above, you can contact the user on your subscription that *does* have these permissions (such as a subscription Owner or Account admin) and proceed in one of the following ways:
* Request that they run the script on your behalf
* Request that they elevate your role on the subscription so that you will have the permissions to proceed yourself. Whether this is appropriate depends on your organization and your role within it.

## Running the sample

To run the sample, start by downloading the *deploy.ps1* file to your machine.

The sample can be run in either [Azure Cloud Shell](https://shell.azure.com) or a local [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli?view=azure-cli-latest) window.

### Setup with Azure Cloud Shell

If using Azure Cloud Shell to run the script, here are the setup steps before running the script.

1. Go to [Azure Cloud Shell](https://shell.azure.com) in a browser. Make sure *Select environment* dropdown in the navigation bar is set to *PowerShell*.

2. Log into the Cloud Shell session by running the following command:

    ```azurecli
    az login
    ```

3. Upload the *deploy.ps1* file that you downloaded earlier to the Cloud Shell, by selecting the *Upload/Download* icon in the navigation bar.

    ![Screenshot for uploading a file to Azure Cloud Shell](/media/scripts/cloud-shell-upload-file.png)

    Find the file in the upload window and hit "Open."

### Script run modes

The script will request user input as it moves through a predefined list of resources to deploy. Follow the prompts, providing new names for resources when requested. You can also respond to the prompts with the names of existing resources to bypass creation of a new resource for that prompt.

The *deploy.ps1* script can be run in two modes. use either of the commands below to run your preferred mode of the script.

* Run command: `.\deploy.ps1`

    This mode of the script is intended to completely deploy an Azure Digital Twins instance, including setting up the required role assignment. It accompanies the following documentation: [*How-to: Set up an instance and authentication (scripted)*](https://docs.microsoft.com/azure/digital-twins/how-to-set-up-instance-scripted).

    Running the script in this mode creates the following resources:
    - A resource group
    - An Azure Digital Twins instance
    - A role assignment of *Azure Digital Twins Owner (Preview)* on the Azure Digital Twins instance for the user that's signed into Cloud Shell. *For potential difficulty with this step, see [Known issues](#known-issues) section below.*

* Run command: `.\deploy.ps1 -RegisterAadApp`

    This is a switch that can be added to create additional resources. In addition to everything completed by the first mode, this mode of the script sets up an Azure Active Directory app registration that can be used with some authentication methods for client apps to access the Azure Digital Twins APIs.

    Running the script in this mode creates the following resources:
    - Everything from the first mode of the script (resource group, instance, role assignment)
    - An Azure Active Directory application registration for client apps that want to use one to authenticate to the Azure Digital Twins APIs

* Run command: `.\deploy.ps1 -endtoend`

    This is a switch that can be added to create additional resources. In addition to everything completed by the first mode, this mode of the script is intended to set up additional Azure resources that can be used along with your Azure Digital Twins instance to set up an end-to-end solution with live data flow. It accompanies the following documentation: [*Tutorial: Connect an end-to-end solution*](https://docs.microsoft.com/azure/digital-twins/tutorial-code).

    Running the script in this mode creates the following resources:
    - Everything from the first mode of the script (resource group, instance, role assignment)
    - An IoT hub
    - An Event Grid topic
    - An Event Grid endpoint in the Azure Digital Twins instance
    - A route to the endpoint in the Azure Digital Twins instance
    - A storage account for Azure Functions
    - An Azure Functions app
    - A system-managed identity for the Azure function to use
    - An Azure function for data ingress
    - An Azure function for processing data through the Azure Digital Twins instance
    - An Event Grid subscription for data ingress. *For potential difficulty with this step, see [Known issues](#known-issues) section below.*
    - An Event Grid subscription for data processing. *For potential difficulty with this step, see [Known issues](#known-issues) section below.*

>[!NOTE]
>The tutorial that accompanies the script's `-endtoend` mode ([*Tutorial: Connect an end-to-end solution*](https://docs.microsoft.com/azure/digital-twins/tutorial-code)) does not rely on the script as part of the tutorial flow. Instead, the tutorial document includes steps to set up each of these resources manually. You can use the script and tutorial instructions together to build a custom solution as you see fit.

* You can also use both of the switches together (`.\deploy.ps1 -RegisterAadApp -endtoend`) to create all possible resources available in the script.

## Re-running the sample

In the event of an erroring input that aborts the script before it's finished with its entire flow, there is a built-in mechanism to allow you to re-run the script and pick up where you left off, without duplicating all the resources already created in early steps.

As you run the script, it stores information about the resources being created in a *config.json* file that is added to the script's directory.

Every time the script is run, it checks this config file to see what steps have already been completed, and will skip over these to pick up where you left off on the previous run. The user is prompted for any required information that's not present in *config.json*.

If you want to run the script from the beginning, **including situations where you want to use the script for a second deployment after a successful first deployment**, delete this config file. You can use the command *rm config.json*.

![Screenshot of removing config file from the directory](/media/scripts/rm-config-file.png)

## Known issues

### Failure to set up role assignment for MSA users

The role assignment step of the script may currently fail for users logged in with a personal [Microsoft account (MSA)](https://account.microsoft.com/account). This is not outputted as a script failure, but will impact creation of other resources down the road.

To determine whether your role assignment was successfully set up after running the script, see instructions in the [*Verify role assignment* section](https://docs.microsoft.com/azure/digital-twins/how-to-set-up-instance-scripted#verify-user-role-assignment) of [*How-to: Set up an instance and authentication (scripted)*](https://docs.microsoft.com/azure/digital-twins/how-to-set-up-instance-scripted) for Azure Digital Twins.

For instructions on how to set up a role assignment manually, see the corresponding setup instructions for either of the following methods:
* [CLI](https://docs.microsoft.com/azure/digital-twins/how-to-set-up-instance-cli#set-up-user-access-permissions)
* [portal](https://docs.microsoft.com/azure/digital-twins/how-to-set-up-instance-portal#set-up-user-access-permissions)

### Failure to create Event Grid subscription in -endtoend mode

As a result of Event Grid CLI support limitations, the script may fail to create an Event Grid subscription. This may happen because the files are still being written while the command is executing.

To resolve, you can wait a few minutes and try again, or complete this step using the manual Azure portal instructions:
* [Event Grid ingress subscription](https://docs.microsoft.com/azure/digital-twins/tutorial-end-to-end#connect-the-iot-hub-to-the-azure-function)
* [Event Grid processing subscription](https://docs.microsoft.com/azure/digital-twins/tutorial-end-to-end#connect-the-function-to-event-grid)

## Next steps

For more information on using this script to set up your Azure Digital Twins instance, including verification steps for the instance and permissions you've set up, see [*How-to: Set up an instance and authentication (scripted)*](https://docs.microsoft.com/azure/digital-twins/how-to-set-up-instance-scripted) in the Azure Digital Twins documentation.

Once your deployment is complete, you can use your new Azure Digital Twins instance to [*connect an end-to-end solution*](https://docs.microsoft.com/azure/digital-twins/tutorial-code). This tutorial uses the resources created by running the script in `-endtoend` mode.
