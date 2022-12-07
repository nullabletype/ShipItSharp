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