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
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;

namespace ShipItSharp.Console.ConsoleTools
{
    public class ConsoleCommandInteraction : ICommandInteraction
    {
        private readonly ILanguageProvider _languageProvider;

        public ConsoleCommandInteraction(ILanguageProvider languageProvider)
        {
            _languageProvider = languageProvider;
        }

        public IEnumerable<int> SelectDeployProjects(DeployConfig config, IList<Project> projects)
        {
            var runner = new InteractiveRunner(
                string.Format(GetTargetAwareString("DeployingTo", "DeployingToMachine", config.MachineName), config.Channel, config.Environment.Name, config.MachineName),
                _languageProvider.GetString(LanguageSection.UiStrings, "PackageNotSelectable"),
                _languageProvider,
                _languageProvider.GetString(LanguageSection.UiStrings, "ProjectName"),
                _languageProvider.GetString(LanguageSection.UiStrings, "CurrentRelease"),
                _languageProvider.GetString(LanguageSection.UiStrings, "CurrentPackage"),
                _languageProvider.GetString(LanguageSection.UiStrings, "NewPackage"),
                _languageProvider.GetString(LanguageSection.UiStrings, "OldestPackagePublish"),
                _languageProvider.GetString(LanguageSection.UiStrings, "PackageAgeDays")
            );

            foreach (var project in projects)
            {
                var packagesAvailable = (project.AvailablePackages.Count > 0) && project.AvailablePackages.All(p => p.SelectedPackage != null);
                DateTime? lastModified = null;

                foreach (var package in project.AvailablePackages)
                {
                    if (((lastModified == null) && (package.SelectedPackage != null) && package.SelectedPackage.PublishedOn.HasValue) ||
                        ((package.SelectedPackage != null) && package.SelectedPackage.PublishedOn.HasValue && (package.SelectedPackage.PublishedOn < lastModified)))
                    {
                        lastModified = package.SelectedPackage.PublishedOn;
                    }
                }

                runner.AddRow(
                    project.Checked,
                    packagesAvailable,
                    project.ProjectName,
                    project.CurrentRelease.Version,
                    project.AvailablePackages.Count > 1 ? _languageProvider.GetString(LanguageSection.UiStrings, "Multi") : project.CurrentRelease.DisplayPackageVersion,
                    project.AvailablePackages.Count > 1 ? _languageProvider.GetString(LanguageSection.UiStrings, "Multi") :
                        packagesAvailable ? project.AvailablePackages.First().SelectedPackage.Version : string.Empty,
                    lastModified.HasValue ? $"{lastModified.Value.ToShortDateString()} : {lastModified.Value.ToShortTimeString()}" : string.Empty,
                    lastModified.HasValue ? $"{DateTime.Now.Subtract(lastModified.Value).Days}{(lastModified.Value < DateTime.Now.AddDays(-7) ? "*" : string.Empty)}" : string.Empty
                );
            }

            runner.Run();
            return runner.GetSelectedIndexes();
        }

        public IEnumerable<int> SelectPromotionProjects(PromotionConfig config, IList<Project> currentProjects, IList<Project> targetProjects)
        {
            var runner = new InteractiveRunner(
                string.Format(GetTargetAwareString("PromotingTo", "PromotingToMachine", config.MachineName), config.SourceEnvironment.Name, config.DestinationEnvironment.Name, config.MachineName),
                _languageProvider.GetString(LanguageSection.UiStrings, "PackageNotSelectable"),
                _languageProvider,
                _languageProvider.GetString(LanguageSection.UiStrings, "ProjectName"),
                _languageProvider.GetString(LanguageSection.UiStrings, "OnSource"),
                _languageProvider.GetString(LanguageSection.UiStrings, "OnTarget")
            );

            foreach (var project in currentProjects)
            {
                var packagesAvailable = project.CurrentRelease != null;
                runner.AddRow(project.Checked, packagesAvailable, project.ProjectName, project.CurrentRelease.Version, targetProjects.FirstOrDefault(p => p.ProjectId == project.ProjectId)?.CurrentRelease?.Version);
            }

            runner.Run();
            return runner.GetSelectedIndexes();
        }

        public IEnumerable<int> SelectDeploySpecificProjects(DeploySpecificConfig config, IList<Project> currentProjects, IList<Release> targetReleases)
        {
            var runner = new InteractiveRunner(
                string.Format(GetTargetAwareString("DeployingSpecificRelease", "DeployingSpecificReleaseToMachine", config.MachineName), config.ReleaseName, config.DestinationEnvironment.Name, config.MachineName),
                _languageProvider.GetString(LanguageSection.UiStrings, "ReleaseNotSelectable"),
                _languageProvider,
                _languageProvider.GetString(LanguageSection.UiStrings, "ProjectName"),
                _languageProvider.GetString(LanguageSection.UiStrings, "OnTarget"),
                _languageProvider.GetString(LanguageSection.UiStrings, "ReleaseToDeploy")
            );

            foreach (var project in currentProjects)
            {
                var release = targetReleases.FirstOrDefault(r => r.ProjectId == project.ProjectId);
                runner.AddRow(project.Checked, release != null, project.ProjectName, project.CurrentRelease.Version, release?.Version);
            }

            runner.Run();
            return runner.GetSelectedIndexes();
        }

        public string Prompt(string prompt)
        {
            return McMaster.Extensions.CommandLineUtils.Prompt.GetString(prompt);
        }

        public string PromptRequired(string prompt)
        {
            string value;
            do
            {
                value = McMaster.Extensions.CommandLineUtils.Prompt.GetString(prompt);
            } while (string.IsNullOrEmpty(value));

            return value;
        }

        public bool Confirm(string prompt, bool defaultValue)
        {
            return McMaster.Extensions.CommandLineUtils.Prompt.GetYesNo(prompt, defaultValue);
        }

        private string GetTargetAwareString(string environmentOnlyKey, string machineKey, string machineName)
        {
            return string.IsNullOrEmpty(machineName)
                ? _languageProvider.GetString(LanguageSection.UiStrings, environmentOnlyKey)
                : _languageProvider.GetString(LanguageSection.UiStrings, machineKey);
        }
    }
}
