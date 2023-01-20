# ![logo](assets/logo.png)  ShipItSharp
ShipItSharp is a command line tool designed to make managing multiple project setups in [Octopus Deploy](https://octopus.com/) easier.

![GitHub Workflow Status](https://img.shields.io/github/workflow/status/nullabletype/ShipItSharp/ShipItSharp%20Console%20Build?logo=github)

## Supported features
Most commands will allow filtering by project group and channel where appropriate.

- Create releases and deploy or promote to environments
- Create and deploy packages to environments from profile files
- Run as a service to run profiled deployments (windows only)
- Rename releases across projects
- Cleanup old channels with no packages
- Force release variable update
- Supports both interactive and non-interactive command execution

## How to use
ShipItSharp is bundled as an executable for your platform. Download from the releases section, mark as executable if your OS requires and execute from your favourite terminal to get started.
Use `--help` after any command to see contextual help.

## Current Status
ShipItSharp is currently in Alpha.