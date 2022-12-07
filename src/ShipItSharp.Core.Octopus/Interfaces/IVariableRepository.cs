using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Models.Variables;

namespace ShipItSharp.Core.Octopus.Interfaces
{
    public interface IVariableRepository
    {
        Task UpdateVariableSet(VariableSet varSet);
    }
}