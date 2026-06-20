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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;

namespace ShipItSharp.Core.JobRunners
{
    public class DeployWithProfileDirectoryRunner
    {
        private readonly IJobRunner _jobRunner;
        private readonly ILanguageProvider _languageProvider;

        public DeployWithProfileDirectoryRunner(IJobRunner jobRunner, ILanguageProvider languageProvider)
        {
            _jobRunner = jobRunner;
            _languageProvider = languageProvider;
        }


        public async Task<int> Run(DeployWithProfileDirectoryConfig config)
        {
            try
            {
                if (!Directory.Exists(config.Directory))
                {
                    Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "PathDoesntExist"));
                    return -1;
                }

                if (config.MonitorDirectory)
                {
                    var hostBuilder = CreateProfileDirectoryHostBuilder(config);

                    await hostBuilder.Build().RunAsync();
                }
                else
                {
                    await RunProfiles(config);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "UnexpectedError"), e.Message);
            }

            return 0;
        }

        private IHostBuilder CreateProfileDirectoryHostBuilder(DeployWithProfileDirectoryConfig config)
        {
            return OperatingSystem.IsWindows()
                ? CreateWindowsProfileDirectoryHostBuilder(config)
                : Host.CreateDefaultBuilder().ConfigureServices((_, services) =>
                    services.AddHostedService(_ => new DeployWithProfileDirectoryService(this, config)));
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private IHostBuilder CreateWindowsProfileDirectoryHostBuilder(DeployWithProfileDirectoryConfig config)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureLogging(
                    options => options.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information))
                .ConfigureServices((_, services) =>
                {
                    services.AddHostedService(_ => new DeployWithProfileDirectoryService(this, config))
                        .Configure<EventLogSettings>(configObject =>
                        {
                            configObject.LogName = "ShipItSharp";
                            configObject.SourceName = "ShipItSharp";
                        });
                })
                .UseWindowsService();
        }

        private async Task RunProfiles(DeployWithProfileDirectoryConfig config, CancellationToken cancellationToken = default)
        {
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var file in Directory.GetFiles(config.Directory, "*auto.profile"))
                {
                    Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "DeployingUsingConfig"), file);
                    await _jobRunner.StartJob(file, null, null, config.ForceRedeploy);
                }

                if (config.MonitorDirectory)
                {
                    Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "SleepingForSeconds"), config.MonitorDelay.ToString());
                    await Task.Delay(config.MonitorDelay * 1000, cancellationToken);
                }

            } while (config.MonitorDirectory && !cancellationToken.IsCancellationRequested);
        }

        private class DeployWithProfileDirectoryService : BackgroundService
        {
            private readonly DeployWithProfileDirectoryRunner _runner;
            private readonly DeployWithProfileDirectoryConfig _config;

            public DeployWithProfileDirectoryService(DeployWithProfileDirectoryRunner runner, DeployWithProfileDirectoryConfig config)
            {
                _runner = runner;
                _config = config;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                await _runner.RunProfiles(_config, stoppingToken);
            }
        }
    }
}
