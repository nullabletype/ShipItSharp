using System.Collections.Generic;
using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Models;

namespace ShipItSharp.Core.Octopus.Interfaces
{
    public interface IEnvironmentRepository
    {
        Task<List<Environment>> GetEnvironments();
        Task<IEnumerable<Environment>> GetEnvironments(string[] idOrNames);
        Task<List<Environment>> GetMatchingEnvironments(string keyword, bool extactMatch = false);
        Task<Environment> CreateEnvironment(string name, string description);
        Task<Environment> GetEnvironment(string idOrName);
        Task DeleteEnvironment(string idOrhref);
    }
}