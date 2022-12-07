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