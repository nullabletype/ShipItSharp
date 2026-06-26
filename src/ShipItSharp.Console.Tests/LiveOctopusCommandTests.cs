using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.Client;
using Octopus.Client.Model;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace ShipItSharp.Console.Tests;

[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class LiveOctopusCommandTests
{
    private const int CommandTimeoutSeconds = 240;
    private const string UrlEnvironmentVariable = "SHIPITSHARP_OCTOPUS_URL";
    private const string ApiKeyEnvironmentVariable = "SHIPITSHARP_OCTOPUS_API_KEY";
    private const string FixturePrefix = "ShipItSharp-LiveTests-";
    private const string MissingGroupFilter = "ShipItSharp-LiveTests-NoProjects";
    private const string SampleProjectName = "Sample Project";

    private static readonly string RunId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
    private static readonly int VersionSeed = (int) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000);
    private static readonly ConcurrentQueue<ExecutedCommandLog> ExecutionLogs = new();

    private string _url;
    private string _apiKey;
    private IOctopusAsyncClient _client;
    private EnvironmentResource _sourceEnvironment;
    private EnvironmentResource _destinationEnvironment;
    private EnvironmentResource _promotionEnvironment;
    private LifecycleResource _lifecycle;
    private ProjectGroupResource _sampleProjectGroup;
    private ProjectResource _templateProject;
    private ProjectResource _sampleProject;
    private ChannelResource _sampleChannel;
    private PackageSelection _samplePackage;
    private TeamResource _team;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _url = Environment.GetEnvironmentVariable(UrlEnvironmentVariable);
        _apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(_url) || string.IsNullOrWhiteSpace(_apiKey))
        {
            Assert.Ignore($"Set {UrlEnvironmentVariable} and {ApiKeyEnvironmentVariable} to run live Octopus command tests.");
        }

        var endpoint = new OctopusServerEndpoint(_url, _apiKey);
        _client = await OctopusAsyncClient.Create(endpoint);
        var currentUser = await _client.Repository.Users.GetCurrent();
        Assert.That(currentUser, Is.Not.Null, "The supplied Octopus API key must be valid.");

        await DeleteStaleFixtureProjectsAndGroups();
        await DeleteStaleFixtureEnvironments();
        await LoadSampleProjectFixture();
        await DeleteStaleFixtureChannels();
        _team = (await _client.Repository.Teams.FindAll(CancellationToken.None)).FirstOrDefault();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        // Trigger HTML compilation right here:
        await WriteHtmlExecutionReport();

        if (_client == null) return;

        await DeleteStaleFixtureChannels();
        await DeleteStaleFixtureProjectsAndGroups();
        await DeleteStaleFixtureEnvironments();
    }

    [Test]
    public async Task LiveSetup_ValidatesInstanceAndFixture()
    {
        var environments = await _client.Repository.Environments.GetAll(CancellationToken.None);

        Assert.That(environments.Select(env => env.Id), Does.Contain(_sourceEnvironment.Id));
        Assert.That(environments.Select(env => env.Id), Does.Contain(_destinationEnvironment.Id));
        Assert.That(environments.Select(env => env.Id), Does.Contain(_promotionEnvironment.Id));
        Assert.That(_sampleProject.Name, Does.StartWith(FixturePrefix));
        Assert.That(_samplePackage.Version, Is.Not.Empty);
    }

    [Test]
    public async Task EnvCommands_CreateListShowAttachAndDeleteDisposableEnvironment()
    {
        var name = $"{FixturePrefix}{RunId}-Disposable";
        EnvironmentResource disposable = null;

        try
        {
            var ensure = await RunShipIt("env", "ensure", "-n", name, "-d", "Created by ShipItSharp live command tests");
            AssertCommandSucceeded(ensure);

            disposable = await FindEnvironment(name);
            Assert.That(disposable, Is.Not.Null);

            var list = await RunShipIt("env");
            AssertCommandSucceeded(list);
            Assert.That(list.Output, Does.Contain(name));

            var show = await RunShipIt("env", "show", "-e", name, "-g", MissingGroupFilter);
            AssertCommandSucceeded(show);

            if (_lifecycle != null)
            {
                var addToLifecycle = await RunShipIt("env", "addtolifecycle", "-e", disposable.Id, "-l", _lifecycle.Id, "-p", "1");
                AssertCommandSucceeded(addToLifecycle);
            }

            if (_team != null)
            {
                var addToTeam = await RunShipIt("env", "addtoteam", "-e", disposable.Id, "-t", _team.Id);
                AssertCommandSucceeded(addToTeam);
            }

            var delete = await RunShipIt("env", "delete", "-e", disposable.Id, "-s");
            AssertCommandSucceeded(delete);

            disposable = await FindEnvironment(name);
            Assert.That(disposable, Is.Null);
        }
        finally
        {
            await DeleteEnvironment(disposable);
        }
    }

    [Test]
    public async Task DeployCommand_CanBuildProfileAgainstLiveInstance()
    {
        var profilePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"shipitsharp-live-save-{RunId}.json");

        var result = await RunShipIt(
            "deploy",
            "-n",
            "-e", _sourceEnvironment.Name,
            "-c", _sampleChannel.Name,
            "-g", _sampleProjectGroup.Name,
            "-s", profilePath);

        AssertCommandSucceeded(result);
        Assert.That(File.Exists(profilePath), Is.True);
        var profile = await File.ReadAllTextAsync(profilePath);
        Assert.That(profile, Does.Contain(_sourceEnvironment.Name));
        Assert.That(profile, Does.Contain("ProjectDeployments"));
    }

    [Test]
    public async Task DeployPromoteAndReleaseCommands_DeploySampleProjectAndValidateOutput()
    {
        var sourceReleaseVersion = $"1.0.{VersionSeed}";
        var directoryReleaseVersion = $"1.0.{VersionSeed}.1";
        var renamedReleaseVersion = $"1.1.{VersionSeed}";
        var profileDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"shipitsharp-profiles-{RunId}");
        Directory.CreateDirectory(profileDirectory);
        var sourceProfilePath = Path.Combine(profileDirectory, "sample-source.profile");
        var directoryProfilePath = Path.Combine(profileDirectory, "sample-directory.auto.profile");
        await WriteSampleDeploymentProfile(sourceProfilePath, _sourceEnvironment, sourceReleaseVersion);
        await WriteSampleDeploymentProfile(directoryProfilePath, _sourceEnvironment, directoryReleaseVersion);

        var profileDeploy = await RunShipIt("deploy", "profile", "-f", sourceProfilePath, "-r");
        AssertCommandSucceeded(profileDeploy);
        AssertDeploymentOutput(profileDeploy, _sourceEnvironment, sourceReleaseVersion);
        var sourceRelease = await GetSampleRelease(sourceReleaseVersion);
        await AssertDeploymentExists(_sourceEnvironment, sourceRelease);

        var profileDirectoryDeploy = await RunShipIt("deploy", "profiledirectory", "-d", profileDirectory, "-r");
        AssertCommandSucceeded(profileDirectoryDeploy);
        AssertDeploymentOutput(profileDirectoryDeploy, _sourceEnvironment, directoryReleaseVersion);

        var deploySpecific = await RunShipIt(
            "deploy",
            "specific",
            "-n",
            "-e", _destinationEnvironment.Name,
            "-r", sourceReleaseVersion,
            "-g", _sampleProjectGroup.Name);
        AssertCommandSucceeded(deploySpecific);
        AssertDeploymentOutput(deploySpecific, _destinationEnvironment, sourceReleaseVersion);
        await AssertDeploymentExists(_destinationEnvironment, sourceRelease);

        var promote = await RunShipIt(
            "promote",
            "-n",
            "-s", _destinationEnvironment.Name,
            "-e", _promotionEnvironment.Name,
            "-g", _sampleProjectGroup.Name,
            "--updatevariables");
        AssertCommandSucceeded(promote);
        AssertDeploymentOutput(promote, _promotionEnvironment, sourceReleaseVersion);
        await AssertDeploymentExists(_promotionEnvironment, sourceRelease);

        var updateVariables = await RunShipIt("release", "updatevariables", "-e", _sourceEnvironment.Name, "-g", _sampleProjectGroup.Name, "-s");
        AssertCommandSucceeded(updateVariables);
        Assert.That(updateVariables.Output, Does.Contain(_sampleProject.Name));

        var rename = await RunShipIt(
            "release",
            "rename",
            "-n",
            "-e", _sourceEnvironment.Name,
            "-r", renamedReleaseVersion,
            "-g", _sampleProjectGroup.Name);
        AssertCommandSucceeded(rename);
        Assert.That(rename.Output, Does.Contain(_sampleProject.Name));
        Assert.That((await GetSampleRelease(renamedReleaseVersion)).Version, Is.EqualTo(renamedReleaseVersion));
    }

    [Test]
    public async Task ReleaseCommand_ShowsHelp()
    {
        AssertCommandSucceeded(await RunShipIt("release"));
    }

    [Test]
    public async Task VariableCommand_AcceptsEmptyProfileAgainstLiveInstance()
    {
        var profilePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"shipitsharp-vars-{RunId}.json");
        await File.WriteAllTextAsync(profilePath, @"{""VariableSets"":[]}");

        var result = await RunShipIt("var", "profile", "-f", profilePath);

        AssertCommandSucceeded(result);
    }

    [Test]
    public async Task ChannelCommand_CleanupRemovesOnlyChannelsWithoutMatchingPackagesAgainstLiveInstance()
    {
        AssertCommandSucceeded(await RunShipIt("channel"));

        var activeChannelName = $"{FixturePrefix}{RunId}-Active";
        var staleChannelName = $"{FixturePrefix}{RunId}-Stale";
        var activeChannel = await CreateSampleProjectChannel(activeChannelName, _samplePackage.Version);
        var staleChannel = await CreateSampleProjectChannel(staleChannelName, "9999.0.0");

        try
        {
            var testMode = await RunShipIt(
                "channel",
                "cleanup",
                "-g", _sampleProjectGroup.Name,
                "-t",
                "--maxpackagesperproject:1");

            AssertCommandSucceeded(testMode);
            Assert.That(testMode.Output, Does.Contain(staleChannelName));
            Assert.That(testMode.Output, Does.Not.Contain(activeChannelName));
            Assert.That(await GetSampleProjectChannel(activeChannelName), Is.Not.Null);
            Assert.That(await GetSampleProjectChannel(staleChannelName), Is.Not.Null);

            var cleanup = await RunShipIt(
                "channel",
                "cleanup",
                "-g", _sampleProjectGroup.Name,
                "--maxpackagesperproject:1");

            AssertCommandSucceeded(cleanup);
            Assert.That(cleanup.Output, Does.Contain(staleChannelName));
            Assert.That(cleanup.Output, Does.Not.Contain(activeChannelName));
            Assert.That(await GetSampleProjectChannel(activeChannelName), Is.Not.Null);
            Assert.That(await GetSampleProjectChannel(staleChannelName), Is.Null);
        }
        finally
        {
            await DeleteChannelIfExists(activeChannel);
            await DeleteChannelIfExists(staleChannel);
        }
    }

    private async Task<EnvironmentResource> EnsureEnvironment(string name)
    {
        var existing = await FindEnvironment(name);
        if (existing != null)
        {
            return existing;
        }

        return await _client.Repository.Environments.Create(
            new EnvironmentResource
            {
                Name = name,
                Description = "Created by ShipItSharp live command tests"
            },
            CancellationToken.None);
    }

    private async Task<EnvironmentResource> FindEnvironment(string name)
    {
        return await _client.Repository.Environments.FindOne(
            environment => environment.Name == name,
            CancellationToken.None);
    }

    private async Task DeleteEnvironment(EnvironmentResource environment)
    {
        if (environment == null)
        {
            return;
        }

        var current = await FindEnvironment(environment.Name);
        if (current == null)
        {
            return;
        }

        await RemoveEnvironmentFromLifecycles(current.Id);
        await _client.Repository.Environments.Delete(current, CancellationToken.None);
    }

    private async Task DeleteStaleFixtureEnvironments()
    {
        var environments = await _client.Repository.Environments.GetAll(CancellationToken.None);
        foreach (var environment in environments.Where(env => env.Name.StartsWith(FixturePrefix, StringComparison.Ordinal)))
        {
            await DeleteEnvironment(environment);
        }
    }

    private async Task DeleteStaleFixtureProjectsAndGroups()
    {
        var projects = await _client.Repository.Projects.GetAll(CancellationToken.None);
        foreach (var project in projects.Where(project => project.Name.StartsWith(FixturePrefix, StringComparison.Ordinal)))
        {
            await _client.Repository.Projects.Delete(project, CancellationToken.None);
        }

        var projectGroups = await _client.Repository.ProjectGroups.GetAll(CancellationToken.None);
        foreach (var projectGroup in projectGroups.Where(group => group.Name.StartsWith(FixturePrefix, StringComparison.Ordinal)))
        {
            await _client.Repository.ProjectGroups.Delete(projectGroup, CancellationToken.None);
        }
    }

    private async Task DeleteStaleFixtureChannels()
    {
        var channels = await GetSampleProjectChannels();
        foreach (var channel in channels.Where(channel => channel.Name.StartsWith(FixturePrefix, StringComparison.Ordinal)))
        {
            await DeleteChannelIfExists(channel);
        }
    }

    private async Task<ChannelResource> CreateSampleProjectChannel(string name, string minimumVersion)
    {
        var channel = new ChannelResource
        {
            Name = name,
            ProjectId = _sampleProject.Id,
            LifecycleId = _sampleProject.LifecycleId,
            Description = "Created by ShipItSharp live command tests"
        };

        var deploymentProcess = await _client.Repository.DeploymentProcesses.Get(_sampleProject.DeploymentProcessId, CancellationToken.None);
        var actions = deploymentProcess.Steps.SelectMany(step => step.Actions).ToArray();
        channel.AddRule($"[{minimumVersion},)", null, actions);

        return await _client.Repository.Channels.Create(channel, CancellationToken.None);
    }

    private async Task<ChannelResource> GetSampleProjectChannel(string name)
    {
        return (await GetSampleProjectChannels()).FirstOrDefault(channel => channel.Name == name);
    }

    private async Task<List<ChannelResource>> GetSampleProjectChannels()
    {
        var channels = await _client.List<ChannelResource>(
            _sampleProject.Link("Channels"),
            new { take = 9999 },
            CancellationToken.None);

        return channels.Items.ToList();
    }

    private async Task DeleteChannelIfExists(ChannelResource channel)
    {
        if (channel == null)
        {
            return;
        }

        var current = await GetSampleProjectChannel(channel.Name);
        if (current == null)
        {
            return;
        }

        await _client.Repository.Channels.Delete(current, CancellationToken.None);
    }

    private async Task LoadSampleProjectFixture()
    {
        _templateProject = await _client.Repository.Projects.FindOne(project => project.Name == SampleProjectName, CancellationToken.None);
        Assert.That(_templateProject, Is.Not.Null, $"Expected Octopus project '{SampleProjectName}' to exist.");

        _sampleProjectGroup = await _client.Repository.ProjectGroups.Create(
            new ProjectGroupResource
            {
                Name = $"{FixturePrefix}{RunId}-ProjectGroup"
            },
            CancellationToken.None);

        _sampleProject = await _client.Repository.Projects.Create(
            new ProjectResource
            {
                Name = $"{FixturePrefix}{RunId}-SampleProject",
                ProjectGroupId = _sampleProjectGroup.Id,
                LifecycleId = _templateProject.LifecycleId,
                ClonedFromProjectId = _templateProject.Id
            },
            CancellationToken.None);

        await CopyTemplateDeploymentProcessToFixtureProject();

        _lifecycle = await _client.Repository.Lifecycles.Get(_sampleProject.LifecycleId, CancellationToken.None);
        Assert.That(_lifecycle.Phases, Is.Not.Empty, $"Project '{_sampleProject.Name}' lifecycle must contain at least one phase.");

        var lifecycleEnvironmentIds = _lifecycle.Phases
            .SelectMany(phase => phase.OptionalDeploymentTargets.Concat(phase.AutomaticDeploymentTargets))
            .Distinct()
            .ToList();
        Assert.That(lifecycleEnvironmentIds.Count, Is.GreaterThanOrEqualTo(3),
            $"Project '{_sampleProject.Name}' lifecycle must have at least three existing environments for deploy, deploy-specific, and promote live tests.");
        _sourceEnvironment = await _client.Repository.Environments.Get(lifecycleEnvironmentIds[0], CancellationToken.None);
        _destinationEnvironment = await _client.Repository.Environments.Get(lifecycleEnvironmentIds[1], CancellationToken.None);
        _promotionEnvironment = await _client.Repository.Environments.Get(lifecycleEnvironmentIds[2], CancellationToken.None);

        _sampleChannel = await _client.Repository.Channels.FindByName(_sampleProject, "Default");
        Assert.That(_sampleChannel, Is.Not.Null, $"Project '{_sampleProject.Name}' must have a Default channel.");

        _samplePackage = await FindLatestSamplePackage();
    }

    private async Task CopyTemplateDeploymentProcessToFixtureProject()
    {
        var templateProcess = await _client.Repository.DeploymentProcesses.Get(_templateProject.DeploymentProcessId, CancellationToken.None);
        var fixtureProcess = await _client.Repository.DeploymentProcesses.Get(_sampleProject.DeploymentProcessId, CancellationToken.None);
        fixtureProcess.Steps.Clear();
        foreach (var step in templateProcess.Steps)
        {
            fixtureProcess.Steps.Add(step);
        }
        await _client.Repository.DeploymentProcesses.Modify(fixtureProcess, CancellationToken.None);
    }

    private async Task<PackageSelection> FindLatestSamplePackage()
    {
        var deploymentProcess = await _client.Repository.DeploymentProcesses.Get(_sampleProject.DeploymentProcessId, CancellationToken.None);
        foreach (var step in deploymentProcess.Steps)
        {
            foreach (var action in step.Actions)
            {
                if (!action.Properties.TryGetValue("Octopus.Action.Package.FeedId", out var feedId) ||
                    feedId.Value != "feeds-builtin" ||
                    !action.Properties.TryGetValue("Octopus.Action.Package.PackageId", out var packageId) ||
                    string.IsNullOrWhiteSpace(packageId.Value))
                {
                    continue;
                }

                var template = (await _client.Repository.Feeds.Get("feeds-builtin", CancellationToken.None)).Links["SearchTemplate"];
                var packages = await _client.Get<PackageFromBuiltInFeedResource[]>(
                    template,
                    new
                    {
                        packageId = packageId.Value,
                        partialMatch = false,
                        includeMultipleVersions = true,
                        take = 1,
                        includePreRelease = true,
                        versionRange = _sampleChannel.Rules.FirstOrDefault()?.VersionRange,
                        preReleaseTag = _sampleChannel.Rules.FirstOrDefault()?.Tag
                    },
                    CancellationToken.None);

                var package = packages.FirstOrDefault();
                if (package != null)
                {
                    return new PackageSelection(packageId.Value, package.Version, action.Name, step.Id);
                }
            }
        }

        Assert.Fail($"Project '{_sampleProject.Name}' must have at least one package in the built-in feed.");
        return null;
    }

    private async Task RemoveEnvironmentFromLifecycles(string environmentId)
    {
        var lifecycles = await _client.Repository.Lifecycles.FindAll(CancellationToken.None);
        foreach (var lifecycle in lifecycles)
        {
            var changed = false;
            foreach (var phase in lifecycle.Phases)
            {
                changed |= phase.OptionalDeploymentTargets.Remove(environmentId);
                changed |= phase.AutomaticDeploymentTargets.Remove(environmentId);
            }

            if (changed)
            {
                await _client.Repository.Lifecycles.Modify(lifecycle, CancellationToken.None);
            }
        }
    }

    private async Task WriteSampleDeploymentProfile(string path, EnvironmentResource environment, string releaseVersion)
    {
        var profile = new
        {
            ProjectDeployments = new[]
            {
                new
                {
                    ProjectId = _sampleProject.Id,
                    ProjectName = _sampleProject.Name,
                    Packages = new[]
                    {
                        new
                        {
                            PackageName = _samplePackage.Version,
                            PackageId = _samplePackage.PackageId,
                            StepName = _samplePackage.ActionName,
                            StepId = _samplePackage.StepId
                        }
                    },
                    ChannelId = _sampleChannel.Id,
                    ChannelVersionRange = _sampleChannel.Rules.FirstOrDefault()?.VersionRange,
                    ChannelVersionTag = _sampleChannel.Rules.FirstOrDefault()?.Tag,
                    ChannelName = _sampleChannel.Name,
                    ReleaseVersion = releaseVersion,
                    LifeCycleId = _sampleProject.LifecycleId
                }
            },
            EnvironmentName = environment.Name,
            EnvironmentId = environment.Id,
            ChannelName = _sampleChannel.Name,
            DeployAsync = true,
            FallbackToDefaultChannel = false
        };

        await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(profile, Formatting.Indented));
    }

    private async Task<ReleaseResource> GetSampleRelease(string version)
    {
        return await _client.Repository.Projects.GetReleaseByVersion(_sampleProject, version, CancellationToken.None);
    }

    private async Task AssertDeploymentExists(EnvironmentResource environment, ReleaseResource release)
    {
        var deployment = await _client.Repository.Deployments.FindOne(
            resource =>
                resource.ProjectId == _sampleProject.Id &&
                resource.EnvironmentId == environment.Id &&
                resource.ReleaseId == release.Id,
            pathParameters: new
            {
                take = 1,
                projects = _sampleProject.Id,
                environments = environment.Id
            },
            cancellationToken: CancellationToken.None,
            path: null);

        Assert.That(deployment, Is.Not.Null, $"Expected release {release.Version} to be deployed to {environment.Name}.");
    }

    private void AssertDeploymentOutput(CommandResult result, EnvironmentResource environment, string releaseVersion)
    {
        Assert.That(result.Output, Does.Contain(_sampleProject.Name));
        Assert.That(result.Output, Does.Contain(environment.Name));
        Assert.That(result.Output, Does.Contain(releaseVersion));
        Assert.That(result.Output, Does.Contain("done"));
        Assert.That(result.Output, Does.Not.Contain("Deployment failed"));
    }

    private static async Task<CommandResult> RunShipIt(params string[] arguments)
    {
        var stopwatch = Stopwatch.StartNew();
        var capturedLines = new ConcurrentQueue<string>();

        var process = new Process
        {
            StartInfo =
            {
                FileName = "dotnet",
                WorkingDirectory = TestContext.CurrentContext.TestDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true, // <--- 1. We claim Stdin
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "ShipIt.dll"));
        foreach (var arg in arguments) process.StartInfo.ArgumentList.Add(arg);

        // 2. Event-driven streaming (Bypasses the EOF Zombie trap)
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            capturedLines.Enqueue(e.Data);
            TestContext.Progress.WriteLine($"[out] {e.Data}"); // <--- 3. LIVE console echo
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            capturedLines.Enqueue(e.Data);
            TestContext.Progress.WriteLine($"[ERR] {e.Data}");
        };

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(CommandTimeoutSeconds));

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // 4. Murder Stdin immediately. If the CLI asks for human input, it will instantly throw an IOException and die.
        process.StandardInput.Close(); 

        try
        {
            await process.WaitForExitAsync(timeout.Token);
            
            // CRITICAL .NET QUIRK: WaitForExitAsync resolves when the process dies, 
            // NOT when the async event-pump finishes draining the last few lines. 
            // Calling the synchronous overload afterwards forces the thread to flush.
            process.WaitForExit(); 
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }

            var partialLog = string.Join(Environment.NewLine, capturedLines);
            throw new TimeoutException($"ShipIt CLI timed out at {CommandTimeoutSeconds}s.\n\n=== LAST CAPTURED LOGS BEFORE HANG ===\n{partialLog}");
        }

        stopwatch.Stop();
        var fullOutput = string.Join(Environment.NewLine, capturedLines);

        ExecutionLogs.Enqueue(new ExecutedCommandLog(
            DateTimeOffset.UtcNow,
            $"dotnet ShipIt.dll {string.Join(" ", arguments)}",
            process.ExitCode,
            fullOutput,
            stopwatch.Elapsed
        ));

        return new CommandResult(process.ExitCode, fullOutput);
    }

    private static async Task WriteHtmlExecutionReport()
    {
        var reportPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"ShipIt-ExecutionReport-{RunId}.html");
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html><html lang='en'><head><meta charset='utf-8'>");
        sb.AppendLine($"<title>ShipIt Test Run {RunId}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; margin: 0; padding: 2rem; }");
        sb.AppendLine(".wrap { max-width: 1100px; margin: 0 auto; }");
        sb.AppendLine("h1 { color: #38bdf8; margin-bottom: 0.2rem; }");
        sb.AppendLine(".meta { color: #94a3b8; font-size: 0.9rem; margin-bottom: 2rem; }");
        sb.AppendLine("details { background: #1e293b; border: 1px solid #334155; border-radius: 6px; margin-bottom: 0.75rem; overflow: hidden; }");
        sb.AppendLine("summary { padding: 0.8rem 1rem; cursor: pointer; font-family: monospace; font-size: 0.95rem; display: flex; justify-content: space-between; align-items: center; user-select: none; }");
        sb.AppendLine("summary:hover { background: #334155; }");
        sb.AppendLine(".cmd { font-weight: 600; color: #e2e8f0; }");
        sb.AppendLine(".badge { padding: 2px 8px; border-radius: 4px; font-size: 0.75rem; font-weight: bold; margin-left: 12px; font-family: sans-serif; }");
        sb.AppendLine(".pass { background: #059669; color: #ecfdf5; }");
        sb.AppendLine(".fail { background: #e11d48; color: #fff1f2; }");
        sb.AppendLine("pre { margin: 0; padding: 1rem; background: #020617; color: #cbd5e1; font-family: 'Consolas', monospace; font-size: 0.85rem; line-height: 1.45; overflow-x: auto; border-top: 1px solid #334155; white-space: pre-wrap; word-break: break-all; }");
        sb.AppendLine(".time { color: #64748b; font-size: 0.8rem; }");
        sb.AppendLine("</style></head><body><div class='wrap'>");

        sb.AppendLine($"<h1>🐙 ShipIt CLI Live Test Report</h1>");
        sb.AppendLine($"<div class='meta'>Run ID: <strong>{RunId}</strong> &bull; Finished: {DateTimeOffset.UtcNow:HH:mm:ss} UTC &bull; Commands Executed: {ExecutionLogs.Count}</div>");

        foreach (var log in ExecutionLogs)
        {
            var isSuccess = log.ExitCode == 0;
            var bClass = isSuccess ? "pass" : "fail";
            var bText = isSuccess ? "EXIT: 0" : $"EXIT: {log.ExitCode}";

            // WebUtility is vital here: Octopus logs contain '<' and '>' arrows which will break raw HTML
            var safeCmd = WebUtility.HtmlEncode(log.Command);
            var safeOutput = WebUtility.HtmlEncode(log.Output);

            sb.AppendLine("<details>");
            sb.AppendLine($"  <summary>");
            sb.AppendLine($"    <span class='cmd'>$ {safeCmd}</span>");
            sb.AppendLine($"    <div><span class='time'>{log.Timestamp:HH:mm:ss} ({log.Duration.TotalSeconds:F1}s)</span><span class='badge {bClass}'>{bText}</span></div>");
            sb.AppendLine($"  </summary>");
            sb.AppendLine($"  <pre>{safeOutput}</pre>");
            sb.AppendLine("</details>");
        }

        sb.AppendLine("</div></body></html>");
        await File.WriteAllTextAsync(reportPath, sb.ToString());

        TestContext.WriteLine($"\n=======================================================");
        TestContext.WriteLine($" HTML EXECUTION REPORT GENERATED:");
        TestContext.WriteLine($" {reportPath}");
        TestContext.WriteLine($"=======================================================\n");
    }

    private static void AssertCommandSucceeded(CommandResult result)
    {
        Assert.That(result.ExitCode, Is.EqualTo(0), result.Output);
    }

    private sealed record CommandResult(int ExitCode, string Output);

    private sealed record PackageSelection(string PackageId, string Version, string ActionName, string StepId);

    private sealed record ExecutedCommandLog(DateTimeOffset Timestamp, string Command, int ExitCode, string Output, TimeSpan Duration);
}
