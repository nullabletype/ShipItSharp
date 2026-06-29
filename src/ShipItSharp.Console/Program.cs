#region copyright
// /*
//     ShipItSharp Deployment Coordinator. Provides extra tooling to help
//     deploy software through Octopus Deploy.
// 
//     Copyright (C) 2022  Steven Davies
// 
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
// */
#endregion


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using ShipItSharp.Console.Commands;
using ShipItSharp.Console.Commands.SubCommands;
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.ChangeLogs.Interfaces;
using ShipItSharp.Core.ChangeLogs.TeamCity;
using ShipItSharp.Core.Configuration;
using ShipItSharp.Core.Deployment;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Logging;
using ShipItSharp.Core.Octopus;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Utilities;
using ShipItSharp.Core.VersionChecking;
using ShipItSharp.Core.VersionChecking.GitHub;
using Environment = System.Environment;

namespace ShipItSharp.Console
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var cwd = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            Directory.SetCurrentDirectory(cwd ?? ".");
            args = args.Select(a => a.Replace("action:", "--action")).ToArray();
            AppDomain.CurrentDomain.UnhandledException += HandleException;
            var initResult = await CheckConfigurationAndInit();
            if (!initResult.Item1.Success)
            {
                System.Console.Write(string.Join(Environment.NewLine, initResult.Item1.Errors));
                return -1;
            }
            var container = initResult.Item2;

            var app = new CommandLineApplication
            {
                Name = "ShipIt"
            };
            app.HelpOption("-?|-h|--help");
            app.UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.Throw;
            app.Conventions.UseConstructorInjection(container);
            RegisterCommands(app, container);

            app.OnExecute(() =>
            {
                System.Console.WriteLine(LogoProvider.LogoText);
                app.ShowHelp();
            });

            return app.Execute(args);
        }
        
        private static readonly IReadOnlyList<Action<CommandLineApplication, IServiceProvider>> CommandRegistrations =
            new List<Action<CommandLineApplication, IServiceProvider>>
            {
                (app, provider) =>
                {
                    var deployer = provider.GetService<Deploy>();
                    app.Command(deployer.CommandName, deploy => deployer.Configure(deploy));
                },
                (app, provider) =>
                {
                    var promoter = provider.GetService<Promote>();
                    app.Command(promoter.CommandName, promote => promoter.Configure(promote));
                },
                (app, provider) =>
                {
                    var environment = provider.GetService<Commands.Environment>();
                    app.Command(environment.CommandName, env => environment.Configure(env));
                },
                (app, provider) =>
                {
                    var release = provider.GetService<Release>();
                    app.Command(release.CommandName, env => release.Configure(env));
                },
                (app, provider) =>
                {
                    var variable = provider.GetService<Variable>();
                    app.Command(variable.CommandName, vari => variable.Configure(vari));
                },
                (app, provider) =>
                {
                    var channel = provider.GetService<Channel>();
                    app.Command(channel.CommandName, vari => channel.Configure(vari));
                },
                (app, provider) =>
                {
                    var task = provider.GetService<TaskCommand>();
                    app.Command(task.CommandName, tasks => task.Configure(tasks));
                }
            };

        private static void RegisterCommands(CommandLineApplication app, IServiceProvider provider)
        {
            foreach (var registration in CommandRegistrations)
            {
                registration(app, provider);
            }
        }

        private static void HandleException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is CommandParsingException exception)
            {
                var colorBefore = System.Console.ForegroundColor;
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine($"Error: {exception.Message}");
                System.Console.ForegroundColor = colorBefore;
                System.Console.WriteLine();
                System.Console.WriteLine(new LanguageProvider().GetString(LanguageSection.UiStrings, "CommandNotRecognised"));
                System.Console.WriteLine();
                Environment.Exit(1);
            }
        }

        private static async Task<Tuple<ConfigurationLoadResult, IServiceProvider>> CheckConfigurationAndInit()
        {
            var log = LoggingProvider.GetLogger<Program>();
            log.Info("Attempting IoC configuration...");
            var services = IoC();
            log.Info("Attempting configuration load...");
            var configurationLoadResult = await ConfigurationProvider.LoadConfiguration(ConfigurationProviderTypes.Json, new LanguageProvider()); //todo: fix this!
            if (!configurationLoadResult.Success)
            {
                log.Error("Failed to load config.");
                return new Tuple<ConfigurationLoadResult, IServiceProvider>(configurationLoadResult, services.BuildServiceProvider());
            }

            log.Info("ShipItSharp started...");
            services.AddSingleton(configurationLoadResult.Configuration);
            var cache = new ShipItSharp.Core.Octopus.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
            var octopusHelper = await OctopusHelper.InitAsync(
                configurationLoadResult.Configuration.OctopusUrl,
                configurationLoadResult.Configuration.ApiKey,
                cache,
                configurationLoadResult.Configuration.CacheTimeoutInSeconds);

            services.AddSingleton<ICacheObjects>(cache);
            services.AddSingleton(octopusHelper);
            log.Info("Set configuration and Octopus helper in IoC");

            var serviceProvider = services.BuildServiceProvider();

            var versionChecker = serviceProvider.GetService<IVersionChecker>();
            var checkResult = await versionChecker.GetLatestVersion();

            if (checkResult.NewVersion)
            {
                ShowNewVersionMessage(checkResult, serviceProvider.GetService<ILanguageProvider>());
            }

            return new Tuple<ConfigurationLoadResult, IServiceProvider>(configurationLoadResult, serviceProvider);
        }

        private static void ShowNewVersionMessage(VersionCheckResult checkResult, ILanguageProvider languageProvider)
        {
            System.Console.WriteLine();
            System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "NewVersionAvailable"));
            System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "CurrentVersion"), checkResult.Release.CurrentVersion);
            System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "NewVersion"), checkResult.Release.TagName);
            if ((checkResult.Release.Assets != null) && checkResult.Release.Assets.Any())
            {
                foreach (var asset in checkResult.Release.Assets)
                {
                    System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "DownloadAvailableHere"), asset.Name, asset.DownloadUrl);
                }
            }
            else
            {
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "UpdateAvailableHere"), checkResult.Release.Url);
            }
            if (!string.IsNullOrEmpty(checkResult.Release.ChangeLog))
            {
                System.Console.WriteLine();
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "ChangeLog"));
                System.Console.WriteLine(checkResult.Release.ChangeLog);
            }
            System.Console.WriteLine();
        }

        private static IServiceCollection IoC()
        {
            return new ServiceCollection()
                .AddLogging()
                .AddSingleton<ConfigurationImplementation, JsonConfigurationProvider>()
                .AddSingleton<IDeployer, Deployer>()
                .AddTransient<IChangeLogProvider, TeamCity>()
                .AddTransient<IWebRequestHelper, WebRequestHelper>()
                .AddTransient<IVersionCheckingProvider, GitHubVersionChecker>()
                .AddTransient<IVersionChecker, VersionChecker>()
                .AddTransient<IJobRunner, ConsoleJobRunner>()
                .AddTransient<ICommandInteraction, ConsoleCommandInteraction>()
                .AddTransient<Deploy, Deploy>()
                .AddTransient<Promote, Promote>()
                .AddTransient<Release, Release>()
                .AddTransient<RenameRelease, RenameRelease>()
                .AddTransient<UpdateReleaseVariables, UpdateReleaseVariables>()
                .AddTransient<DeployWithProfile, DeployWithProfile>()
                .AddTransient<DeploySpecific, DeploySpecific>()
                .AddTransient<DeployWithProfileDirectory, DeployWithProfileDirectory>()
                .AddTransient<EnsureEnvironment, EnsureEnvironment>()
                .AddTransient<DeleteEnvironment, DeleteEnvironment>()
                .AddTransient<DisableEnvironment, DisableEnvironment>()
                .AddTransient<EnableEnvironment, EnableEnvironment>()
                .AddTransient<EnvironmentToTeam, EnvironmentToTeam>()
                .AddTransient<EnvironmentToLifecycle, EnvironmentToLifecycle>()
                .AddTransient<ShowEnvironment, ShowEnvironment>()
                .AddTransient<Variable, Variable>()
                .AddTransient<VariablesWithProfile, VariablesWithProfile>()
                .AddTransient<Channel, Channel>()
                .AddTransient<CleanupChannels, CleanupChannels>()
                .AddTransient<TaskCommand, TaskCommand>()
                .AddTransient<PrioritiseTask, PrioritiseTask>()
                .AddTransient<CancelTask, CancelTask>()
                .AddTransient<Commands.Environment, Commands.Environment>()
                .AddTransient<IUiLogger, ConsoleJobRunner>()
                .AddTransient<IProgressBar, ConsoleProgressBar>().AddMemoryCache()
                .AddTransient<ILanguageProvider, LanguageProvider>().AddMemoryCache()
                .AddTransient<DeployWithProfileDirectoryRunner, DeployWithProfileDirectoryRunner>()
                .AddTransient<PromotionRunner, PromotionRunner>()
                .AddTransient<DeployRunner, DeployRunner>()
                .AddTransient<DeploySpecificRunner, DeploySpecificRunner>()
                .AddTransient<ChannelsRunner, ChannelsRunner>()
                .AddTransient<EnvironmentRunner, EnvironmentRunner>()
                .AddTransient<TaskRunner, TaskRunner>()
                .AddTransient<EnsureEnvironmentRunner, EnsureEnvironmentRunner>()
                .AddTransient<DeleteEnvironmentRunner, DeleteEnvironmentRunner>()
                .AddTransient<DisableEnvironmentRunner, DisableEnvironmentRunner>()
                .AddTransient<EnableEnvironmentRunner, EnableEnvironmentRunner>()
                .AddTransient<EnvironmentToTeamRunner, EnvironmentToTeamRunner>()
                .AddTransient<EnvironmentToLifecycleRunner, EnvironmentToLifecycleRunner>()
                .AddTransient<ShowEnvironmentRunner, ShowEnvironmentRunner>()
                .AddTransient<RenameReleaseRunner, RenameReleaseRunner>()
                .AddTransient<UpdateReleaseVariablesRunner, UpdateReleaseVariablesRunner>()
                .AddTransient<VariablesWithProfileRunner, VariablesWithProfileRunner>()
                .AddTransient<ICacheObjects, MemoryCache>();
        }
    }
}
