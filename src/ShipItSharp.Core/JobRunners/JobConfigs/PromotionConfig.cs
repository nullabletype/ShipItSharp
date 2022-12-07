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
    public class PromotionConfig
    {

        private PromotionConfig() { }
        public Environment DestinationEnvironment { get; set; }
        public Environment SourceEnvironment { get; set; }
        public string GroupFilter { get; set; }
        public bool RunningInteractively { get; set; }

        public static Result<PromotionConfig> Create(Environment destEnv, Environment srcEnv, string filter, bool runningInteractively)
        {
            if (destEnv == null || string.IsNullOrEmpty(destEnv.Id))
            {
                return Result.Failure<PromotionConfig>("destiniation environment is not set correctly");
            }

            if (srcEnv == null || string.IsNullOrEmpty(srcEnv.Id))
            {
                return Result.Failure<PromotionConfig>("source environment is not set correctly");
            }

            return Result.Success(new PromotionConfig
            {
                DestinationEnvironment = destEnv,
                SourceEnvironment = srcEnv,
                GroupFilter = filter,
                RunningInteractively = runningInteractively
            });
        }
    }
}