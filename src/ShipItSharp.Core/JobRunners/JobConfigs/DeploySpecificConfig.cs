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
using ShipItSharp.Core.Deployment.Models;

namespace ShipItSharp.Core.JobRunners.JobConfigs
{
    public class DeploySpecificConfig
    {

        private DeploySpecificConfig() { }
        public Environment DestinationEnvironment { get; private set; }
        public string ReleaseName { get; private set; }
        public string GroupFilter { get; private set; }
        public bool RunningInteractively { get; private set; }
        public bool FallbackToDefault { get; private set; }
        public string DefaultFallbackChannel { get; private set; }
        public bool FallbackToDefaultChannel => DefaultFallbackChannel != null;

        public static Result<DeploySpecificConfig> Create(Environment destEnv, string release, string filter, bool runningInteractively, string fallbackToDefaultChannel)
        {
            if (destEnv == null || string.IsNullOrEmpty(destEnv.Id))
            {
                return Result.Failure<DeploySpecificConfig>("destiniation environment is not set correctly");
            }

            if (release == null || string.IsNullOrEmpty(release))
            {
                return Result.Failure<DeploySpecificConfig>("Release name must be specified");
            }

            return Result.Success(new DeploySpecificConfig
            {
                DestinationEnvironment = destEnv,
                ReleaseName = release,
                GroupFilter = filter,
                RunningInteractively = runningInteractively,
                DefaultFallbackChannel = fallbackToDefaultChannel
            });
        }
    }
}