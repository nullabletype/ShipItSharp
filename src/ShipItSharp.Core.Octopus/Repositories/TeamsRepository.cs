using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories;

public class TeamsRepository : ITeamsRepository
{
    private OctopusHelper octopusHelper;

    public TeamsRepository(OctopusHelper octopusHelper)
    {
        this.octopusHelper = octopusHelper;
    }

    public async Task RemoveEnvironmentsFromTeams(string envId) 
    {
        var teams = await octopusHelper.client.Repository.Teams.FindAll(CancellationToken.None);
        foreach (var team in teams) 
        {
            var scopes = await octopusHelper.client.Repository.Teams.GetScopedUserRoles(team);
                
            foreach (var scope in scopes.Where(s => s.EnvironmentIds.Contains(envId))) 
            {
                scope.EnvironmentIds.Remove(envId);
                await octopusHelper.client.Repository.ScopedUserRoles.Modify(scope, CancellationToken.None);
            }
                
        }
    }

    public async Task AddEnvironmentToTeam(string envId, string teamId) 
    {
        var team = await octopusHelper.client.Repository.Teams.Get(teamId, CancellationToken.None);
        var environment = await octopusHelper.client.Repository.Environments.Get(envId, CancellationToken.None);
        if (team == null || environment == null) 
        {
            return;
        }
        var scopes = await octopusHelper.client.Repository.Teams.GetScopedUserRoles(team);
        foreach (var scope in scopes) 
        {
            if (!scope.EnvironmentIds.Contains(envId))
            {
                scope.EnvironmentIds.Add(envId);
                await octopusHelper.client.Repository.ScopedUserRoles.Modify(scope, CancellationToken.None);
            }
        }
    }
}