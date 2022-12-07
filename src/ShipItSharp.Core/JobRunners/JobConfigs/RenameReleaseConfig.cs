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


using CSharpFunctionalExtensions;
using NuGet.Versioning;
using ShipItSharp.Core.Deployment.Models;

namespace ShipItSharp.Core.JobRunners.JobConfigs
{
    public class RenameReleaseConfig
    {

        private RenameReleaseConfig() { }
        public string ReleaseName { get; private set; }
        public bool RunningInteractively { get; private set; }
        public Environment Environment { get; private set; }
        public string GroupFilter { get; private set; }

        public static Result<RenameReleaseConfig> Create(string filter, Environment environment, bool interactive, string releaseName)
        {

            if (environment == null || string.IsNullOrEmpty(environment.Id))
            {
                return Result.Failure<RenameReleaseConfig>("Environment is not set correctly");
            }

            if (!SemanticVersion.TryParse(releaseName, out _))
            {
                return Result.Failure<RenameReleaseConfig>("Release name is not a valid semantic version!");
            }

            return Result.Success(new RenameReleaseConfig
            {
                GroupFilter = filter,
                ReleaseName = releaseName,
                Environment = environment,
                RunningInteractively = interactive
            });
        }
    }
}