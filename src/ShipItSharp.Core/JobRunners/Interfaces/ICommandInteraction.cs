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

using System.Collections.Generic;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.JobRunners.JobConfigs;

namespace ShipItSharp.Core.JobRunners.Interfaces
{
    public interface ICommandInteraction
    {
        IEnumerable<int> SelectDeployProjects(DeployConfig config, IList<Project> projects);
        IEnumerable<int> SelectPromotionProjects(PromotionConfig config, IList<Project> currentProjects, IList<Project> targetProjects);
        IEnumerable<int> SelectDeploySpecificProjects(DeploySpecificConfig config, IList<Project> currentProjects, IList<Release> targetReleases);
        string Prompt(string prompt);
        string PromptRequired(string prompt);
        bool Confirm(string prompt, bool defaultValue);
    }
}
