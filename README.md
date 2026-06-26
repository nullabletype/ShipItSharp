# <img src="assets/logo.svg" alt="ShipItSharp logo" width="48"> ShipItSharp

ShipItSharp is a command line tool for coordinating Octopus Deploy releases across multiple projects, environments, channels, and project groups.

[shipitsharp.app](https://shipitsharp.app)

![GitHub Workflow Status](https://github.com/nullabletype/ShipItSharp/actions/workflows/windows_build.yml/badge.svg?logo=github)

## Status

ShipItSharp is currently in alpha. Expect the command surface and configuration shape to keep evolving.

## Supported features

Most commands support filtering by project group and channel where appropriate.

- Create releases and deploy or promote them to Octopus environments
- Create and deploy packages to environments from profile files
- Run profiled deployments as a Windows service
- Rename releases across projects
- Clean up old channels with no packages
- Force release variable updates
- Run interactively or from scripts with command line options

## Requirements

To use ShipItSharp you need:

- An Octopus Deploy instance
- An Octopus API key with permissions for the projects and environments you want to manage
- A terminal on Windows, macOS, or Linux

To work on the codebase you also need:

- .NET 10 SDK
- Git

## Installation

Download the executable for your platform from the GitHub releases page, then run it from your terminal. On macOS or Linux you may need to mark the file as executable first:

```bash
chmod +x ./ShipIt
```

The console app is named `ShipIt` at runtime. Use `--help` on the root command or any subcommand to see the available options:

```bash
./ShipIt --help
./ShipIt deploy --help
./ShipIt release rename --help
```

## Configuration

ShipItSharp reads a `config.json` file from the executable directory. If no file exists, the app creates a sample `config.json` and exits so you can fill it in.

Minimal configuration:

```json
{
  "OctopusUrl": "https://octopus.example.com",
  "ApiKey": "API-XXXXXXXXXXXXXXXXXXXXXXXX",
  "ProjectGroupFilterString": "",
  "DefaultChannel": "",
  "CacheTimeoutInSeconds": 300,
  "EnableTrace": false
}
```

Useful fields:

- `OctopusUrl`: Base URL for your Octopus Deploy instance.
- `ApiKey`: Octopus API key used by the CLI.
- `ProjectGroupFilterString`: Optional default text used to restrict project group matches.
- `DefaultChannel`: Optional fallback channel used by deploy commands that allow falling back to a default.
- `CacheTimeoutInSeconds`: Octopus object cache duration.
- `EnableTrace`: Enables more verbose diagnostic logging when supported by the command path.

Keep `config.json` out of source control when it contains real credentials.

## Basic CLI usage

Run commands from the directory that contains the `ShipIt` executable and `config.json`.

Common command groups:

```bash
./ShipIt deploy --help
./ShipIt promote --help
./ShipIt env --help
./ShipIt release --help
./ShipIt var --help
./ShipIt channel --help
```

Examples:

```bash
# Interactive deployment flow
./ShipIt deploy

# Non-interactive deployment with explicit environment, channel, and group filter
./ShipIt deploy --noprompt --environment "Test" --channel "Default" --groupfilter "Payments"

# Promote releases from one environment to another
./ShipIt promote --sourcenvironment "Test" --environment "Production" --groupfilter "Payments"

# Rename a release across matching projects
./ShipIt release rename --environment "Production" --releasename "2026.06.26" --groupfilter "Payments"

# Update release variables for an environment
./ShipIt release updatevariables --environment "Production" --groupfilter "Payments"

# Deploy from a saved profile
./ShipIt deploy profile --filepath "./profiles/payments.profile"
```

Command options can also be passed with the short aliases shown in each command's help output.

## Development setup

Clone the repository and restore dependencies:

```bash
git clone https://github.com/nullabletype/ShipItSharp.git
cd ShipItSharp
dotnet restore src/ShipItSharp.sln
```

Build the full solution:

```bash
dotnet build src/ShipItSharp.sln
```

Run the test suite:

```bash
dotnet test src/ShipItSharp.sln
```

Run the console app from source:

```bash
dotnet run --project src/ShipItSharp.Console/ShipItSharp.Console.csproj -- --help
dotnet run --project src/ShipItSharp.Console/ShipItSharp.Console.csproj -- deploy --help
```

The console app changes its working directory to the built executable directory before loading configuration. For local development, run once to let the sample file be generated in the build output directory, or copy a development-only `config.json` next to the built `ShipIt` executable.

## Project layout

- `src/ShipItSharp.Console`: CLI entry point and command registration.
- `src/ShipItSharp.Core`: shared command orchestration and job runners.
- `src/ShipItSharp.Core.Configuration`: configuration loading and validation.
- `src/ShipItSharp.Core.Octopus`: Octopus Deploy client integration.
- `src/ShipItSharp.Core.Deployment`: deployment coordination logic.
- `src/ShipItSharp.Core.Language`: localised UI and option strings.
- `src/*Tests`: NUnit test projects.
- `docs/architecture`: LikeC4 architecture source and preview instructions.

## Releasing

Release builds should be produced from the console project:

```bash
dotnet publish src/ShipItSharp.Console/ShipItSharp.Console.csproj -c Release
```

Copy a real `config.json` next to the published executable before running it against Octopus.

## License

ShipItSharp is licensed under the GNU General Public License. See [LICENSE.txt](LICENSE.txt).
