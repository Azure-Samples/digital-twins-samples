---
page_type: sample
languages:
- csharp
- powershell
products:
- azure-digital-twins
name: Azure Digital Twins end-to-end samples
description: Contains end-to-end project samples for the latest version of Azure Digital Twins.
urlFragment: digital-twins-samples
---

# End-to-end samples for Azure Digital Twins
 
Azure Digital Twins is a developer platform for next-generation IoT solutions that lets you create, run, and manage digital representations of your business environment, securely and efficiently in the cloud. With Azure Digital Twins, creating live operational state representations is quick and cost-effective, and digital representations stay current with real-time data from IoT and other data sources.

For more information about Azure Digital Twins and its key concepts, see the [Azure Digital Twins documentation](https://docs.microsoft.com/azure/digital-twins/).

## Purpose

This project contains 2 samples for working with Azure Digital Twins:
* A **building scenario** sample written in .NET. Can be used to set up and simulate a full end-to-end scenario with Azure Digital Twins
* A **deployment script** written in PowerShell. Can be used to deploy and set [AAD](https://docs.microsoft.com/azure/active-directory/fundamentals/active-directory-whatis) permissions for an Azure Digital Twins instance

## Setup

Get the samples by downloading this repository as a ZIP file to your machine.

## Sample project contents
The sample repo contains:

| File/folder | Description |
| --- | --- |
| `AdtSampleApp` | For the building scenario. Contains a sample client application built to interact with Azure Digital Twins, as well as an Azure Functions app with two functions (*ProcessDTRoutedData* and *ProcessHubToDTEvents*) that are used to route data between Azure Digital Twins and other external services. |
| `DeviceSimulator` | For the building scenario. Simulator for a that generates telemetry events. The simulated device is a thermostat that sends temperature telemetry every ~5 seconds. |
| `scripts` | For the deployment script. Contains *deploy.ps1*.

## Prerequisites

#### For the building scenario:
These samples were developed and expected to run in Visual Studio 2019. Ensure you have installed Visual Studio 2019 version **16.5.1XXX or later** on your development machine. If you have an older version installed already, you can open the Visual Studio Installer app on your machine and follow the prompts to update your installation.

#### For the deployment script:
None

## Instructions

The instructions for working with these samples are included in the [Azure Digital Twins documentation](https://docs.microsoft.com/azure/digital-twins/).

#### Building scenario:
There are two possible sets of instructions for working with this sample.
* [*Tutorial: Explore the basics with a sample client app*](https://docs.microsoft.com/azure/digital-twins/tutorial-command-line-app)
* [*Tutorial: Connect an end-to-end solution*](https://docs.microsoft.com/azure/digital-twins/tutorial-end-to-end)

#### Deployment script:
Instructions for running the script, and manual description of the automated steps within the script, are found in [*How-to: Create an Azure Digital Twins instance*](https://docs.microsoft.com/azure/digital-twins/how-to-set-up-instance).

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
