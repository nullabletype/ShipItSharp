using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Console.Commands;
using ShipItSharp.Console.Commands.SubCommands;
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Deployment;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;
using DeploymentEnvironment = ShipItSharp.Core.Deployment.Models.Environment;

namespace ShipItSharp.Console.Tests;

[TestFixture]
[NonParallelizable]
public class ProjectGroupFilterTests
{
    private static IEnumerable<TestCaseData> FilterCases()
    {
        foreach (var (commandType, arguments) in Commands())
        {
            foreach (var (configured, supplied, expected) in new[]
            {
                ("Payments", (string)null, "Payments"),
                ("Payments", "Orders", "Orders"),
                ((string)null, (string)null, (string)null),
                ("", (string)null, (string)null),
                ("Payments", "", (string)null)
            })
            {
                yield return new TestCaseData(commandType, arguments, configured, supplied, expected)
                    .SetName($"{commandType.Name}_GroupFilter_Config={configured ?? "null"}_Option={supplied ?? "absent"}");
            }
        }
    }

    private static IEnumerable<TestCaseData> InteractiveCases()
    {
        return Commands()
            .Where(command => command.Type == typeof(Deploy) || command.Type == typeof(DeploySpecific) ||
                              command.Type == typeof(Promote) || command.Type == typeof(RenameRelease))
            .Select(command => new TestCaseData(command.Type, command.Arguments));
    }

    [TestCaseSource(nameof(FilterCases))]
    public async Task Execute_ResolvesConfiguredAndExplicitFilters(Type commandType, string[] arguments, string configured, string supplied, string expected)
    {
        var (app, projects, services) = CreateApp(commandType, configured);
        using (services)
        {
            var options = arguments.ToList();
            if (commandType != typeof(ShowEnvironment))
            {
                options.Add("--noprompt");
            }
            if (supplied != null)
            {
                options.AddRange(new[] { "--groupfilter", supplied });
            }

            app.Execute(options.ToArray());

            if (expected == null)
            {
                await projects.DidNotReceiveWithAnyArgs().GetFilteredProjectGroups(default);
            }
            else
            {
                await projects.Received(1).GetFilteredProjectGroups(expected);
                await projects.Received(1).GetFilteredProjectGroups(Arg.Any<string>());
            }
        }
    }

    [TestCaseSource(nameof(InteractiveCases))]
    public async Task Execute_InteractiveCommand_UsesConfiguredFilterWithoutReadingInput(Type commandType, string[] arguments)
    {
        var (app, projects, services) = CreateApp(commandType, "Payments");
        using (services)
        {
            var originalInput = System.Console.In;
            using var input = new UnexpectedInputReader();
            try
            {
                System.Console.SetIn(input);

                app.Execute(arguments);

                await projects.Received(1).GetFilteredProjectGroups("Payments");
            }
            finally
            {
                System.Console.SetIn(originalInput);
            }
        }
    }

    private static IEnumerable<(Type Type, string[] Arguments)> Commands()
    {
        yield return (typeof(Deploy), new[] { "-e", "Prod", "-c", "Default" });
        yield return (typeof(DeploySpecific), new[] { "-e", "Prod", "-r", "1.2.3", "-f", "Default" });
        yield return (typeof(Promote), new[] { "-e", "Prod", "-s", "Test" });
        yield return (typeof(ShowEnvironment), new[] { "-e", "Prod" });
        yield return (typeof(RenameRelease), new[] { "-e", "Prod", "-r", "1.2.3" });
        yield return (typeof(UpdateReleaseVariables), new[] { "-e", "Prod" });
        yield return (typeof(CleanupChannels), new[] { "--maxpackagesperproject:100", "--testmode" });
    }

    private static (CommandLineApplication App, IProjectRepository Projects, ServiceProvider Services) CreateApp(Type commandType, string configured)
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration.ProjectGroupFilterString.Returns(configured);
        var helper = Substitute.For<IOctopusHelper>();
        var projects = Substitute.For<IProjectRepository>();
        helper.Projects.Returns(projects);
        projects.GetProjectStubs().Returns(new List<ProjectStub>());
        projects.GetFilteredProjectGroups(Arg.Any<string>()).Returns(new List<ProjectGroup>());
        helper.Environments.GetEnvironment(Arg.Any<string>()).Returns(call => Task.FromResult(new DeploymentEnvironment
        {
            Id = "Environments-" + call.Arg<string>(),
            Name = call.Arg<string>()
        }));
        helper.Environments.GetMatchingEnvironments(Arg.Any<string>()).Returns(call => Task.FromResult(new List<DeploymentEnvironment>
        {
            new() { Id = "Environments-" + call.Arg<string>(), Name = call.Arg<string>() }
        }));
        var deployer = Substitute.For<IDeployer>();
        deployer.CheckDeployment(Arg.Any<EnvironmentDeployment>()).Returns(new DeploymentCheckResult { Success = true });

        var services = new ServiceCollection()
            .AddSingleton(configuration)
            .AddSingleton(helper)
            .AddSingleton(TestLanguageProvider.Create())
            .AddSingleton(Substitute.For<IProgressBar>())
            .AddSingleton(Substitute.For<ICommandInteraction>())
            .AddSingleton(deployer)
            .AddSingleton(Substitute.For<IUiLogger>())
            .AddSingleton(Substitute.For<IJobRunner>())
            .AddTransient<DeployRunner>()
            .AddTransient<DeploySpecificRunner>()
            .AddTransient<PromotionRunner>()
            .AddTransient<ShowEnvironmentRunner>()
            .AddTransient<RenameReleaseRunner>()
            .AddTransient<UpdateReleaseVariablesRunner>()
            .AddTransient<ChannelsRunner>()
            .AddTransient<DeployWithProfileDirectoryRunner>()
            .AddTransient<DeployWithProfile>()
            .AddTransient<DeployWithProfileDirectory>()
            .AddTransient<DeploySpecific>()
            .BuildServiceProvider();
        var command = (BaseCommand)ActivatorUtilities.CreateInstance(services, commandType);
        var app = new CommandLineApplication();
        command.Configure(app);
        return (app, projects, services);
    }

    private sealed class UnexpectedInputReader : TextReader
    {
        public override string ReadLine() => throw new AssertionException("Configured group filtering must not prompt for input.");
    }
}
