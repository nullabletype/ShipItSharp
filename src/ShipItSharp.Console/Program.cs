﻿#region copyright
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private static int Main(string[] args)
        {
            var cwd = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            Directory.SetCurrentDirectory(cwd ?? ".");
            args = args.Select(a => a.Replace("action:", "--action")).ToArray();
            
            bool verboseMode = GetAndStripVerbosityLevelFromArgs(ref args);
            
            AppDomain.CurrentDomain.UnhandledException += HandleException;
            var initResult = CheckConfigurationAndInit(verboseMode).GetAwaiter().GetResult();
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

            var deployer = container.GetService<Deploy>();
            app.Command(deployer.CommandName, deploy => deployer.Configure(deploy));
            var promoter = container.GetService<Promote>();
            app.Command(promoter.CommandName, promote => promoter.Configure(promote));
            var environment = container.GetService<Commands.Environment>();
            app.Command(environment.CommandName, env => environment.Configure(env));
            var release = container.GetService<Release>();
            app.Command(release.CommandName, env => release.Configure(env));
            var variable = container.GetService<Variable>();
            app.Command(variable.CommandName, vari => variable.Configure(vari));
            var channel = container.GetService<Channel>();
            app.Command(channel.CommandName, vari => channel.Configure(vari));

            app.OnExecute(() =>
            {
                System.Console.WriteLine(LogoProvider.LogoText);
                app.ShowHelp();
            });

            return app.Execute(args);
        }
        
        private static bool GetAndStripVerbosityLevelFromArgs(ref string[] args)
        {
            var tempArgs = args.Where(a => a != "--verbose" && a != "-v").ToArray();

            if (tempArgs.Length < args.Length)
            {
                args = tempArgs;
                return true;
            }
            
            return false;
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
                System.Console.WriteLine("Command wasn't recognised. Try -? for help if you're stuck.");
                System.Console.WriteLine();
                Environment.Exit(1);
            }
        }

        private static async Task<Tuple<ConfigurationLoadResult, IServiceProvider>> CheckConfigurationAndInit(bool verboseMode)
        {
            if (verboseMode)
            {
                LoggingProvider.EnableVerboseLogging();
            }
            var log = LoggingProvider.GetLogger<Program>();
            log.Info("Attempting IoC configuration...");
            var container = IoC(verboseMode);
            log.Info("Attempting configuration load...");
            var configurationLoadResult = await ConfigurationProvider.LoadConfiguration(ConfigurationProviderTypes.Json, new LanguageProvider()); //todo: fix this!
            if (!configurationLoadResult.Success)
            {
                log.Error("Failed to load config.");
                return new Tuple<ConfigurationLoadResult, IServiceProvider>(configurationLoadResult, container.BuildServiceProvider());
            }
            log.Info("ShipItSharp started...");
            OctopusHelper.Init(configurationLoadResult.Configuration.OctopusUrl, configurationLoadResult.Configuration.ApiKey, LoggingProvider.Factory);
            container.AddSingleton(OctopusHelper.Default);
            container.AddSingleton(configurationLoadResult.Configuration);
            log.Info("Set configuration in IoC");

            var serviceProvider = container.BuildServiceProvider();
            //Temporary filth
            serviceProvider.GetService<IOctopusHelper>().SetCacheImplementation(serviceProvider.GetService<ICacheObjects>(), configurationLoadResult.Configuration.CacheTimeoutInSeconds);

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
            System.Console.WriteLine("-------------------------------------");
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
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "ChangeLog"));
                System.Console.WriteLine(checkResult.Release.ChangeLog);
            }
            System.Console.WriteLine("-------------------------------------");
        }

        private static IServiceCollection IoC(bool verboseMode)
        {
            return new ServiceCollection()
                .AddSingleton(LoggingProvider.Factory)
                .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
                .AddSingleton<ConfigurationImplementation, JsonConfigurationProvider>()
                .AddSingleton<IOctopusHelper, OctopusHelper>()
                .AddSingleton<IDeployer, Deployer>()
                .AddTransient<IChangeLogProvider, TeamCity>()
                .AddTransient<IWebRequestHelper, WebRequestHelper>()
                .AddTransient<IVersionCheckingProvider, GitHubVersionChecker>()
                .AddTransient<IVersionChecker, VersionChecker>()
                .AddTransient<IJobRunner, ConsoleJobRunner>()
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
                .AddTransient<EnvironmentToTeam, EnvironmentToTeam>()
                .AddTransient<EnvironmentToLifecycle, EnvironmentToLifecycle>()
                .AddTransient<Variable, Variable>()
                .AddTransient<VariablesWithProfile, VariablesWithProfile>()
                .AddTransient<Channel, Channel>()
                .AddTransient<CleanupChannels, CleanupChannels>()
                .AddTransient<Commands.Environment, Commands.Environment>()
                .AddTransient<IUiLogger, ConsoleJobRunner>()
                .AddTransient<IProgressBar, ConsoleProgressBar>().AddMemoryCache()
                .AddTransient<ILanguageProvider, LanguageProvider>().AddMemoryCache()
                .AddTransient<DeployWithProfileDirectoryRunner, DeployWithProfileDirectoryRunner>()
                .AddTransient<PromotionRunner, PromotionRunner>()
                .AddTransient<DeployRunner, DeployRunner>()
                .AddTransient<DeploySpecificRunner, DeploySpecificRunner>()
                .AddTransient<ChannelsRunner, ChannelsRunner>()
                .AddTransient<ICacheObjects, MemoryCache>();
        }
    }
}