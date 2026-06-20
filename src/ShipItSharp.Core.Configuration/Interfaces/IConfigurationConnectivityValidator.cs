using System.Threading.Tasks;

namespace ShipItSharp.Core.Configuration.Interfaces
{
    public interface IConfigurationConnectivityValidator
    {
        Task ValidateConnectivity(IConfiguration configuration);
    }
}
