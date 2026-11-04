# ShipItSharp project contract

This contract configures a supervised pilot based on Agentic Foundation. Steven owns scope, operator experience, policy, merge and release decisions. The authoritative task is the accepted GitHub issue or explicit instruction in the current chat. The issue template records scope and acceptance evidence. Existing instructions in AGENTS.md govern CLI confirmation safety.

## Product and architecture

ShipItSharp coordinates Octopus releases across projects, environments and channels. Preserve switch conventions, profile compatibility, truthful command results and the confirmation boundary when changing workflows.

Use the current console → job runner → Octopus repository/deployment structure. LikeC4 is already adopted in [docs/architecture](docs/architecture). Update affected diagrams with architecture changes. Record a consequential new decision in a small ADR before implementation. Routine repairs reuse accepted decisions.

For CLI experience changes, reuse command registration and interaction helpers in the console project and the strings in `ShipItSharp.Core.Language`. Specify invocation, prompt/default, cancellation, output and exit code. Provide a synthetic terminal walkthrough for Steven's approval before publishing changed operator interactions or wording. README, generated help and release notes in the PR must describe command surface or operator workflow changes. Website work also needs human review of the actual rendered result.

## Validation from the repository root

The solution targets .NET 10 and uses NUnit with Microsoft.NET.Test.Sdk and NUnit3TestAdapter. Resolve versions from project files and `src/Directory.Build.Props`. Verify current upstream support and security advisories for dependency/toolchain changes. A full support inventory remains pending and is a prerequisite for unattended adoption.

| Check | Command | Required evidence |
| --- | --- | --- |
| Full suite | `dotnet test src/ShipItSharp.sln` | Every code candidate, with live result or skipped count stated separately |
| Safe offline full suite on macOS/Linux | `env -u SHIPITSHARP_OCTOPUS_URL -u SHIPITSHARP_OCTOPUS_API_KEY dotnet test src/ShipItSharp.sln` | Ordinary agent validation without Octopus credentials |
| Focused suite | `dotnet test src/ShipItSharp.Core.Tests/ShipItSharp.Core.Tests.csproj` | Affected project, adapting the path to the existing test project |
| Release build | `dotnet build -c Release src/ShipItSharp.sln` | Build, dependency, packaging or workflow changes |
| Root help | `dotnet run --project src/ShipItSharp.Console/ShipItSharp.Console.csproj -- --help` | CLI surface changes, also exercise the affected subcommand's help |
| Live command suite | `dotnet test src/ShipItSharp.Console.Tests/ShipItSharp.Console.Tests.csproj --filter FullyQualifiedName~ShipItSharp.Console.Tests.LiveOctopusCommandTests` | Changed Octopus workflows, within the live boundary below |
| Patch whitespace | `git diff --check` | Every candidate |

For documentation-only changes, verify local links and commands against source/help and run the whitespace check. A command that builds or runs tests may create ignored outputs. Inspect help startup before executing it against a configured environment.

GitHub currently runs Linux and Windows build/test workflows on push. Capture each required check's actual name, tested SHA, run URL and result after publication. Required-status rules, PR event coverage and platform coverage beyond those workflows remain unverified. Missing platform evidence stays pending.

## Live Octopus boundary

- Offline implementation and independent checks run without Octopus or publication credentials. On Windows, remove both Octopus variables from the child process environment before the offline command.
- Use `SHIPITSHARP_OCTOPUS_URL` and `SHIPITSHARP_OCTOPUS_API_KEY` only for a designated disposable test instance with permission for fixture creation, deployment, cancellation and cleanup. The fixture uses the client's default repository scope and has no explicit space selector. Approval must cover that actual scope. A named non-default space needs supported configuration/code before use. Presence of credentials alone does not establish that the target is approved. Confirm the target's approved purpose without printing credentials.
- Inspect `LiveOctopusCommandTests` before a live run. It needs a suitable Sample Project and targets, and cleans up resources with the shared `ShipItSharp-LiveTests-` prefix during setup and teardown. That prefix spans runs. Serialize the entire fixture against the shared test instance, including runs from other branches, machines and CI.
- Record test-instance approval and availability in the task. With an approved target and credentials, perform required live validation. Otherwise keep its acceptance evidence pending and explain the exact gap.
- Use synthetic fixtures. Private configuration inspection is disabled for this pilot. Keep keys, URLs containing credentials, real project data and execution reports out of Git, PRs and agent evidence. Record sanitised counts/results and cleanup failures. A skipped fixture proves no live behaviour.

## Authority and limits

Agents may inspect, implement, validate, correct and commit locally within the accepted task. Obtain independent agent verification before calling the candidate ready. The reviewer must inspect the candidate directly and must not have authored it. See the delivery workflow for reviewer instructions.

During this pilot, branch push and PR creation/update require an explicit instruction covering publication. Operator experience changes additionally require approval of the local result. Merge, tags, deployment and release each require explicit authority. This implementation request authorises the local setup. It grants no standing unattended publication authority.

Start each bounded task with a maximum of **3 correction cycles and 90 minutes of active work**. A cycle is an implementation correction after failed validation or review. Human/CI waiting time is excluded. Carry the counters across resumption. Stop at the first exhausted limit, repeated unexplained failure, material scope decision or missing permission. Steven may revise limits for a task.

Bound ordinary validation processes to 15 minutes each and the complete live fixture to 30 minutes. On timeout, terminate owned processes, inspect for fixture cleanup needs and report incomplete evidence. These are operator/harness limits to configure before execution, not timeouts enforced by this document.

Use ignored `src/**/bin` and `src/**/obj` for normal build output. Keep any additional task scratch output in an ignored `.agent-work/` directory, with a 100 MiB allowance and a 24-hour lifetime. Retain a sanitised checkpoint there only while a task is open, then remove owned scratch files. Build caches remain tool-managed. Never delete unrelated work to meet a limit.

## Enforcement status and pilot completion

The workflow is supervised guidance. Host capture restrictions, credential isolation, process/storage limits and GitHub protection have not been proven by this adoption. Keep desktop/native-window capture and recording disabled. Review files and outgoing history for secrets and local output before staging or publication.

Current Actions request write permissions at workflow scope, upload build outputs on pushes and produce draft releases on tags. The Linux workflow also deploys Pages from main. Existing automation needs a separate reviewed change before unattended publication, including least-privilege verification, PR checks, release-only payload rules and an audit of live required checks. This contract supplies no additional artifact upload permission.

Complete one bounded code task through independent review, required live evidence, human gates and explicitly authorised publication. Record corrections, escaped defects and pending checks in its PR. Then review this contract with Steven and simplify or strengthen controls based on that result. The queue-race scenario in the local improvement plan is a candidate pilot, subject to accepted issue scope.

Provenance and retained licence are recorded in [docs/agentic-foundation.md](docs/agentic-foundation.md).
