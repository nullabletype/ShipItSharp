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
        private DeployWithProfileDirectoryConfig _currentConfig;

        public DeployWithProfileDirectoryRunner(IJobRunner jobRunner, ILanguageProvider languageProvider)
        {
            _jobRunner = jobRunner;
            _languageProvider = languageProvider;
        }


        public async Task<int> Run(DeployWithProfileDirectoryConfig config)
        {
            _currentConfig = config;
            try
            {
                if (!Directory.Exists(config.Directory))
                {
                    Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "PathDoesntExist"));
                    return -1;
                }

                if (config.MonitorDirectory)
                {
                    var hostBuilder = Host.CreateDefaultBuilder()
                        .ConfigureLogging(
                            options => options.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information))
                        .ConfigureServices((_, services) =>
                        {
                            services.AddHostedService(_ => new DeployWithProfileDirectoryService(this))
                                .Configure<EventLogSettings>(configObject =>
                                {
                                    configObject.LogName = "Sample Service";
                                    configObject.SourceName = "Sample Service Source";
                                });
                        }).UseWindowsService();

                    await hostBuilder.Build().RunAsync();
                }
                else
                {
                    await RunProfiles();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "UnexpectedError"), e.Message);
            }

            return 0;
        }

        private async Task RunProfiles()
        {
            do
            {
                foreach (var file in Directory.GetFiles(_currentConfig.Directory, "*auto.profile"))
                {
                    Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "DeployingUsingConfig"), file);
                    await _jobRunner.StartJob(file, null, null, _currentConfig.ForceRedeploy);
                }

                if (_currentConfig.MonitorDirectory)
                {
                    Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "SleepingForSeconds"), _currentConfig.MonitorDelay.ToString());
                    await Task.Delay(_currentConfig.MonitorDelay * 1000);
                }

            } while (_currentConfig.MonitorDirectory);
        }

        private class DeployWithProfileDirectoryService : BackgroundService
        {
            private readonly DeployWithProfileDirectoryRunner _runner;

            public DeployWithProfileDirectoryService(DeployWithProfileDirectoryRunner runner)
            {
                _runner = runner;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await _runner.RunProfiles();
                }
            }
        }
    }
}