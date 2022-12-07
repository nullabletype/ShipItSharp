using System.Threading.Tasks;

namespace ShipItSharp.Core.Octopus.Interfaces
{
    public interface ITeamsRepository
    {
        Task RemoveEnvironmentsFromTeams(string envId);
        Task AddEnvironmentToTeam(string envId, string teamId);
    }
}