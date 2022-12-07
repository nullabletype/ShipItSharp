#region copyright
/*
    ShipItSharp Deployment Coordinator. Provides extra tooling to help 
    deploy software through Octopus Deploy.

    Copyright (C) 2018  Steven Davies

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
#endregion


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Logging.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Utilities;

namespace ShipItSharp.Console
{
    public class ConsoleJobRunner : IUiLogger, IJobRunner
    {
        private readonly IOctopusHelper _helper;
        private readonly IConfiguration _configuration;
        private readonly IDeployer _deployer;
        private readonly ILanguageProvider _languageProvider;
        private readonly IProgressBar _progressBar;

        public ConsoleJobRunner(IOctopusHelper helper, IDeployer deployer, IProgressBar progressBar, IConfiguration configuration, ILanguageProvider languageProvider)
        {
            this._helper = helper;
            this._deployer = deployer;
            this._progressBar = progressBar;
            this._configuration = configuration;
            this._languageProvider = languageProvider;
        }

        public async Task StartJob(string pathToProfile, string message, string releaseVersion,
            bool forceDeploymentIfSamePackage)
        {
            if (!File.Exists(pathToProfile))
            {
                WriteLine("Couldn't find file at " + pathToProfile);
                return;
            }
            try
            {
                var job =
                    StandardSerialiser.DeserializeFromJsonNet<EnvironmentDeployment>(await File.ReadAllTextAsync(pathToProfile));

                var projects = new List<ProjectDeployment>();

                foreach (var project in job.ProjectDeployments)
                {
                    var octoProject =
                        await
                            _helper.Projects.GetProject(project.ProjectId, job.EnvironmentId,
                                project.ChannelVersionRange, project.ChannelVersionTag);
                    var packages =
                        await _helper.Packages.GetPackages(octoProject.ProjectId, project.ChannelVersionRange, project.ChannelVersionTag);
                    IList<PackageStep> defaultPackages = null;
                    foreach (var package in project.Packages)
                    {
                        if (package.PackageId == "latest")
                        {
                            // Filter to packages specifically for this package step, then update the package versions
                            var availablePackages = packages.Where(pack => pack.StepId == package.StepId);

                            // If there are no packages for this step, check if we've been asked to jump back to default channel.
                            if ((!availablePackages.Any() || availablePackages.First().SelectedPackage == null) && job.FallbackToDefaultChannel && !string.IsNullOrEmpty(_configuration.DefaultChannel))
                            {
                                if (defaultPackages == null)
                                {
                                    var defaultChannel = await _helper.Channels.GetChannelByName(project.ProjectId, _configuration.DefaultChannel);
                                    defaultPackages = await _helper.Packages.GetPackages(project.ProjectId, defaultChannel.VersionRange, defaultChannel.VersionTag);
                                    //  We're now using the default channel, so update the project release to have the correct channel info for the deployment.
                                    project.ChannelId = defaultChannel.Id;
                                    project.ChannelName = defaultChannel.Name;
                                    project.ChannelVersionRange = defaultChannel.VersionRange;
                                    project.ChannelVersionTag = defaultChannel.VersionTag;
                                }
                                availablePackages = defaultPackages.Where(pack => pack.StepId == package.StepId);
                            }

                            var selectedPackage = availablePackages.First().SelectedPackage;

                            if (selectedPackage != null)
                            {
                                package.PackageId = selectedPackage.Id;
                                package.PackageName = selectedPackage.Version;
                                package.StepName = selectedPackage.StepName;
                            }
                            else
                            {
                                System.Console.Out.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NoSuitablePackageFound"), package.StepName, project.ProjectName);
                            }
                        }
                    }
                    if (!forceDeploymentIfSamePackage)
                    {
                        if (!await IsDeploymentRequired(job, project))
                        {
                            continue;
                        }
                    }
                    if (!string.IsNullOrEmpty(message))
                    {
                        project.ReleaseMessage = message;
                    }
                    if (!string.IsNullOrEmpty(releaseVersion))
                    {
                        project.ReleaseVersion = releaseVersion;
                    }
                    projects.Add(project);
                }

                job.ProjectDeployments = projects;

                await _deployer.StartJob(job, this, true);
            }
            catch (Exception e)
            {
                WriteLine("Couldn't deploy! " + e.Message + e.StackTrace);
            }
        }

        public void WriteLine(string toWrite)
        {
            System.Console.WriteLine(toWrite);
        }

        public void WriteStatusLine(string status)
        {
            _progressBar.WriteStatusLine(status);
        }

        public void CleanCurrentLine()
        {
            _progressBar.CleanCurrentLine();
        }

        public void WriteProgress(int current, int total, string message)
        {
            _progressBar.WriteProgress(current, total, message);
        }

        public void StopAnimation()
        {
            _progressBar.StopAnimation();
        }

        private async Task<bool> IsDeploymentRequired(EnvironmentDeployment job, ProjectDeployment project)
        {
            var needsDeploy = false;
            var currentRelease = (await _helper.Releases.GetReleasedVersion(project.ProjectId, job.EnvironmentId)).Release;
            if ((currentRelease != null) && !string.IsNullOrEmpty(currentRelease.Id))
            {
                // Check if we have any packages that are different versions. If they're the same, we don't need to deploy.
                foreach (var package in project.Packages)
                {
                    if (!currentRelease.SelectedPackages.Any(pack => (pack.StepName == package.StepName) && (package.PackageName == pack.Version)))
                    {
                        needsDeploy = true;
                    }
                }
            }
            return needsDeploy;
        }
    }
}