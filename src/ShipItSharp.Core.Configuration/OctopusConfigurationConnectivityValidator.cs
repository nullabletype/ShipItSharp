using System.Threading.Tasks;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Octopus;

namespace ShipItSharp.Core.Configuration
{
    public class OctopusConfigurationConnectivityValidator : IConfigurationConnectivityValidator
    {
        public async Task ValidateConnectivity(IConfiguration configuration)
        {
            var octoHelper = await OctopusHelper.CreateAsync(configuration.OctopusUrl, configuration.ApiKey);
            await octoHelper.Environments.GetEnvironments();
        }
    }
}
