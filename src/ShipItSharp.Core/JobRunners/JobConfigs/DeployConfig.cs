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
using CSharpFunctionalExtensions;
using ShipItSharp.Core.Deployment.Models;
using Environment = ShipItSharp.Core.Deployment.Models.Environment;

namespace ShipItSharp.Core.JobRunners.JobConfigs
{
    public class DeployConfig
    {

        private DeployConfig() { }
        public Channel Channel { get; private set; }
        public Channel DefaultFallbackChannel { get; private set; }
        public Environment Environment { get; private set; }
        public string GroupFilter { get; private set; }
        public bool RunningInteractively { get; private set; }
        public bool ForceRedeploy { get; private set; }
        public string SaveProfile { get; private set; }
        public string ReleaseName { get; private set; }
        public bool FallbackToDefaultChannel => DefaultFallbackChannel != null;

        public static Result<DeployConfig> Create(Environment env, Channel channel, Channel defaultFallbackChannel, string filter, string saveProfile, bool runningInteractively, bool forceRedeploy = false)
        {
            if (env == null || string.IsNullOrEmpty(env.Id))
            {
                return Result.Failure<DeployConfig>("destiniation environment is not set correctly");
            }

            if (channel == null || string.IsNullOrEmpty(channel.Id))
            {
                return Result.Failure<DeployConfig>("channel is not set correctly");
            }

            if ((defaultFallbackChannel != null) && string.IsNullOrEmpty(defaultFallbackChannel.Id))
            {
                return Result.Failure<DeployConfig>("default channel is not set correctly");
            }

            if (!string.IsNullOrEmpty(saveProfile))
            {
                try
                {
                    if (!File.Exists(saveProfile))
                    {
                        using (File.Create(saveProfile, 1, FileOptions.DeleteOnClose)) { }
                    }
                }
                catch (Exception e)
                {
                    return Result.Failure<DeployConfig>("path to save profile is not set correctly: " + e.Message);
                }
            }

            return Result.Success(new DeployConfig
            {
                Environment = env,
                Channel = channel,
                DefaultFallbackChannel = defaultFallbackChannel,
                SaveProfile = saveProfile,
                GroupFilter = filter,
                RunningInteractively = runningInteractively,
                ForceRedeploy = forceRedeploy
            });
        }
    }
}