using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.Octopus.Interfaces
{
    public interface ILifeCycleRepository
    {
        Task<LifeCycle> GetLifeCycle(string idOrHref);
        Task RemoveEnvironmentsFromLifecycles(string envId);
        Task<(bool Success, LifecycleErrorType ErrorType, string Error)> AddEnvironmentToLifecyclePhase(string envId, string lcId, int phaseId, bool automatic);
    }
}